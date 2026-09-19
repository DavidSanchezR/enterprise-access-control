using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad entre <c>contracts/org-units.yaml</c> y lo que la API publica (Historia 2, RF-007,
/// RF-008, RF-043 a RF-045).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class UnidadesOrganizativasContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("org-units.yaml");

    private static readonly Dictionary<string, string> EsquemasEquivalentes = new(StringComparer.Ordinal)
    {
        ["UnidadOrganizativa"] = "UnidadOrganizativaDto",
        ["UnidadOrganizativaRequest"] = "UnidadOrganizativaRequest",
        ["UnidadOrganizativaUpdateRequest"] = "UnidadOrganizativaUpdateRequest",
        ["NodoArbol"] = "NodoArbolDto",
    };

    [Fact]
    public void Toda_operacion_del_contrato_existe_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Fact]
    public void La_api_no_expone_operaciones_de_unidades_fuera_del_contrato()
    {
        var enLaApi = fixture.DocumentoApi.Paths()
            .EnumerateObject()
            .Where(p => p.Name.StartsWith("/api/unidades-organizativas", StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Select(m => (Ruta: p.Name, Metodo: m.Name)))
            .ToList();

        enLaApi.Should().BeEquivalentTo(Contrato.Operaciones);
    }

    [Theory]
    [InlineData("/api/unidades-organizativas", "get")]
    [InlineData("/api/unidades-organizativas", "post")]
    [InlineData("/api/unidades-organizativas/arbol", "get")]
    [InlineData("/api/unidades-organizativas/{id}", "get")]
    [InlineData("/api/unidades-organizativas/{id}", "put")]
    [InlineData("/api/unidades-organizativas/{id}/mover", "post")]
    public void La_api_declara_todos_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void Los_esquemas_del_contrato_coinciden_en_sus_propiedades_con_los_de_la_api()
    {
        foreach (var (enContrato, enApi) in EsquemasEquivalentes)
        {
            fixture.DocumentoApi.TieneEsquema(enApi).Should().BeTrue(
                "el contrato declara {0}, que la API serializa como {1}", enContrato, enApi);

            fixture.DocumentoApi.PropiedadesDeEsquema(enApi)
                .Should().BeEquivalentTo(
                    Contrato.PropiedadesPorEsquema[enContrato],
                    porque => porque.WithoutStrictOrdering(),
                    "el esquema {0} debe tener la misma forma que {1}", enContrato, enApi);
        }
    }

    [Theory]
    [InlineData("/api/unidades-organizativas")]
    [InlineData("/api/unidades-organizativas/arbol")]
    public void CompaniaPrincipalId_es_obligatorio_al_listar_y_al_pedir_el_arbol(string ruta)
    {
        // RF-043: hay varias Compañías Principales simultáneas, cada una con su árbol aislado. Una
        // consulta sin Principal no tendría un resultado definible y devolvería datos cruzados.
        var parametro = fixture.DocumentoApi.Operacion(ruta, "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "companiaPrincipalId");

        parametro.GetProperty("required").GetBoolean().Should().BeTrue(
            "{0} no debe poder consultarse sin indicar la Compañía Principal", ruta);
    }

    [Fact]
    public void La_creacion_acepta_companiaPrincipalId_para_el_nodo_raiz()
    {
        // Obligatorio solo cuando unidadSuperiorId es null; el contrato no puede expresar esa
        // condicionalidad en el esquema, así que la propiedad existe y el servicio la exige.
        fixture.DocumentoApi.PropiedadesDeEsquema("UnidadOrganizativaRequest")
            .Should().Contain(["nombre", "unidadSuperiorId", "companiaPrincipalId", "estado"]);
    }

    [Fact]
    public void La_creacion_solo_declara_obligatorios_los_campos_que_exige_el_contrato()
    {
        // contracts/org-units.yaml: required: [nombre, estado]. Publicar los identificadores como
        // obligatorios forzaría a un cliente generado a enviar "unidadSuperiorId": null para crear
        // un nodo raíz — una fricción que el contrato no pide.
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("UnidadOrganizativaRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().BeEquivalentTo(["nombre", "estado"]);
    }

    [Fact]
    public void La_actualizacion_no_permite_cambiar_la_jerarquia_ni_la_compania()
    {
        // contracts/org-units.yaml: el update solo lleva nombre y estado. Aceptar aquí
        // unidadSuperiorId o companiaPrincipalId permitiría reubicar un nodo —o cambiar su
        // Principal propietaria— esquivando las validaciones de /mover (RF-038, RF-045).
        fixture.DocumentoApi.PropiedadesDeEsquema("UnidadOrganizativaUpdateRequest")
            .Should().BeEquivalentTo(["nombre", "estado"]);
    }

    [Fact]
    public void El_nodo_del_arbol_es_recursivo()
    {
        var hijos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("NodoArbolDto")
            .GetProperty("properties").GetProperty("hijos");

        hijos.GetProperty("type").GetString().Should().Be("array");
        hijos.GetProperty("items").GetProperty("$ref").GetString()
            .Should().EndWith("NodoArbolDto");
    }

    [Fact]
    public void La_unidad_expone_su_compania_principal_resuelta_por_el_servidor()
    {
        // RF-044: la entidad no tiene columna hacia Compañía; el DTO sí la expone, resuelta
        // recorriendo hasta la raíz. Que esté en el contrato de salida y no en el de entrada es la
        // diferencia que sostiene la regla.
        fixture.DocumentoApi.PropiedadesDeEsquema("UnidadOrganizativaDto")
            .Should().Contain("companiaPrincipalId");
    }
}
