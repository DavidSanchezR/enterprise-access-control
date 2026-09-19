using EnterpriseAccessControl.Application.Common.Abstractions;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.Hierarchy;

/// <summary>
/// Semántica de detección de ciclos y resolución de raíz (RF-038, Principio V), sobre grafos en
/// memoria y sin base de datos.
/// </summary>
/// <remarks>
/// La implementación real resuelve el recorrido con un CTE recursivo de SQL Server, que se ejercita
/// en las pruebas de integración. Lo que se fija aquí es el <em>contrato</em> de
/// <see cref="IJerarquiaConsultas"/>: qué debe considerarse ciclo y qué debe devolverse como raíz.
/// Una implementación en memoria del mismo contrato permite cubrir casos límite —autorreferencia,
/// cadenas profundas, hermanos— sin pagar el arranque de un contenedor por cada uno.
/// </remarks>
public sealed class JerarquiaCicloValidatorTests
{
    /// <summary>Jerarquía en memoria: hijo → padre.</summary>
    private sealed class JerarquiaEnMemoria(Dictionary<Guid, Guid?> padres) : IJerarquiaConsultas
    {
        public Task<bool> CrearíaCicloAsync(
            string tabla, string columnaPadre, Guid nodoId, Guid nuevoPadreId, CancellationToken ct = default)
        {
            if (nodoId == nuevoPadreId)
            {
                return Task.FromResult(true);
            }

            var ancestros = Ancestros(nuevoPadreId);
            return Task.FromResult(ancestros.Contains(nodoId));
        }

        public Task<IReadOnlyList<Guid>> ObtenerAncestrosAsync(
            string tabla, string columnaPadre, Guid nodoId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Ancestros(nodoId));

        public Task<Guid> ObtenerRaizAsync(
            string tabla, string columnaPadre, Guid nodoId, CancellationToken ct = default)
        {
            var ancestros = Ancestros(nodoId);
            return Task.FromResult(ancestros.Count == 0 ? nodoId : ancestros[^1]);
        }

        private List<Guid> Ancestros(Guid nodoId)
        {
            var resultado = new List<Guid>();
            var visitados = new HashSet<Guid> { nodoId };
            var actual = nodoId;

            while (padres.TryGetValue(actual, out var padre) && padre is not null)
            {
                // Sin la guarda, un ciclo ya presente en los datos colgaría el recorrido.
                if (!visitados.Add(padre.Value))
                {
                    break;
                }

                resultado.Add(padre.Value);
                actual = padre.Value;
            }

            return resultado;
        }
    }

    private static readonly Guid Raiz = Guid.CreateVersion7();
    private static readonly Guid Hijo = Guid.CreateVersion7();
    private static readonly Guid Nieto = Guid.CreateVersion7();
    private static readonly Guid Hermano = Guid.CreateVersion7();
    private static readonly Guid OtraRaiz = Guid.CreateVersion7();

    /// <summary>Raíz → Hijo → Nieto; Raíz → Hermano; OtraRaiz aislada.</summary>
    /// <remarks>
    /// Se devuelve la interfaz y no el tipo concreto a propósito: lo que estas pruebas fijan es el
    /// contrato de <see cref="IJerarquiaConsultas"/>, no los detalles del doble en memoria. El coste
    /// de despacho por interfaz que señala CA1859 es irrelevante en una prueba.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "El tipo de la interfaz documenta que la prueba cubre el contrato, no la implementación.")]
    private static IJerarquiaConsultas Arbol() => new JerarquiaEnMemoria(new()
    {
        [Raiz] = null,
        [Hijo] = Raiz,
        [Nieto] = Hijo,
        [Hermano] = Raiz,
        [OtraRaiz] = null,
    });

    [Fact]
    public async Task Un_nodo_no_puede_ser_su_propio_padre()
    {
        // Es el ciclo más corto posible y debe detectarse sin consultar la jerarquía.
        (await Arbol().CrearíaCicloAsync("T", "P", Hijo, Hijo)).Should().BeTrue();
    }

    [Fact]
    public async Task Mover_un_nodo_bajo_su_propio_hijo_crearia_un_ciclo()
    {
        (await Arbol().CrearíaCicloAsync("T", "P", Hijo, Nieto)).Should().BeTrue();
    }

    [Fact]
    public async Task Mover_un_nodo_bajo_un_descendiente_lejano_crearia_un_ciclo()
    {
        (await Arbol().CrearíaCicloAsync("T", "P", Raiz, Nieto)).Should().BeTrue();
    }

    [Fact]
    public async Task Mover_un_nodo_bajo_su_hermano_no_crea_ciclo()
    {
        (await Arbol().CrearíaCicloAsync("T", "P", Hijo, Hermano)).Should().BeFalse();
    }

    [Fact]
    public async Task Mover_un_nodo_bajo_un_ancestro_no_crea_ciclo()
    {
        // Subir en el árbol es legítimo: el ciclo lo produce bajar hacia un descendiente.
        (await Arbol().CrearíaCicloAsync("T", "P", Nieto, Raiz)).Should().BeFalse();
    }

    [Fact]
    public async Task Mover_un_nodo_a_otro_arbol_no_crea_ciclo()
    {
        // Sin ciclo, pero prohibido por otra regla: RF-045 impide cambiar de Compañía Principal.
        // Son comprobaciones distintas y el validador de jerarquía no debe opinar sobre la segunda.
        (await Arbol().CrearíaCicloAsync("T", "P", Hijo, OtraRaiz)).Should().BeFalse();
    }

    [Fact]
    public async Task Los_ancestros_se_devuelven_del_mas_cercano_a_la_raiz()
    {
        var ancestros = await Arbol().ObtenerAncestrosAsync("T", "P", Nieto);

        ancestros.Should().Equal(Hijo, Raiz);
    }

    [Fact]
    public async Task Un_nodo_raiz_no_tiene_ancestros()
    {
        (await Arbol().ObtenerAncestrosAsync("T", "P", Raiz)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_raiz_de_un_nodo_profundo_es_el_ancestro_mas_alto()
    {
        (await Arbol().ObtenerRaizAsync("T", "P", Nieto)).Should().Be(Raiz);
    }

    [Fact]
    public async Task La_raiz_de_un_nodo_sin_padre_es_el_propio_nodo()
    {
        (await Arbol().ObtenerRaizAsync("T", "P", Raiz)).Should().Be(Raiz);
    }

    [Fact]
    public async Task Cada_arbol_resuelve_su_propia_raiz()
    {
        var arbol = Arbol();

        (await arbol.ObtenerRaizAsync("T", "P", Hermano)).Should().Be(Raiz);
        (await arbol.ObtenerRaizAsync("T", "P", OtraRaiz)).Should().Be(OtraRaiz);
    }
}
