using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad de los endpoints de árbol de <c>contracts/area-access.yaml</c>
/// (Historia 6, RF-009, RF-038, RF-046).
/// </summary>
/// <remarks>
/// El contrato incluye además <c>/{id}/tipos-persona</c>, que pertenece a la Historia 7: esta prueba
/// se limita a las operaciones del árbol para que la conformidad de US6 pueda verificarse sin
/// implementar US7.
/// </remarks>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class AreasAccesoContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato =
        ApiContratoFixture.CargarContrato("area-access.yaml");

    /// <summary>Operaciones del árbol de áreas (Historia 6).</summary>
    private static readonly (string Ruta, string Metodo)[] OperacionesUs6 =
    [
        ("/api/areas-acceso", "get"),
        ("/api/areas-acceso", "post"),
        ("/api/areas-acceso/arbol", "get"),
        ("/api/areas-acceso/{id}", "get"),
        ("/api/areas-acceso/{id}", "put"),
        ("/api/areas-acceso/{id}/mover", "post"),
    ];

    [Fact]
    public void El_contrato_declara_las_operaciones_del_arbol()
    {
        Contrato.Operaciones.Should().Contain(OperacionesUs6);
    }

    [Fact]
    public void Todas_las_operaciones_del_arbol_existen_en_la_api()
    {
        var faltantes = OperacionesUs6
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/areas-acceso", "get")]
    [InlineData("/api/areas-acceso", "post")]
    [InlineData("/api/areas-acceso/arbol", "get")]
    [InlineData("/api/areas-acceso/{id}", "get")]
    [InlineData("/api/areas-acceso/{id}", "put")]
    [InlineData("/api/areas-acceso/{id}/mover", "post")]
    public void La_api_declara_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void Los_esquemas_del_area_coinciden_en_sus_propiedades()
    {
        foreach (var (enContrato, enApi) in new Dictionary<string, string>(StringComparer.Ordinal)
                 {
                     ["AreaAcceso"] = "AreaAccesoDto",
                     ["AreaAccesoRequest"] = "AreaAccesoRequest",
                     ["AreaAccesoUpdateRequest"] = "AreaAccesoUpdateRequest",
                     ["NodoArbol"] = "NodoAreaDto",
                 })
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
    [InlineData("/api/areas-acceso")]
    [InlineData("/api/areas-acceso/arbol")]
    public void CompaniaPrincipalId_es_obligatorio_al_listar_y_al_pedir_el_arbol(string ruta)
    {
        // RF-043/RF-046: cada Principal tiene su propio árbol de áreas. Una consulta sin Principal
        // no tendría un resultado definible y devolvería datos cruzados entre compañías.
        var parametro = fixture.DocumentoApi.Operacion(ruta, "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "companiaPrincipalId");

        parametro.GetProperty("required").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void La_creacion_solo_declara_obligatorios_los_campos_que_exige_el_contrato()
    {
        // contracts/area-access.yaml: required: [nombre, estado]. companiaPrincipalId lo exige el
        // servicio solo cuando areaSuperiorId es null, una condicionalidad que JSON Schema no
        // expresa.
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("AreaAccesoRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().BeEquivalentTo(["nombre", "estado"]);
    }

    [Fact]
    public void La_creacion_acepta_companiaPrincipalId_para_el_area_raiz()
    {
        fixture.DocumentoApi.PropiedadesDeEsquema("AreaAccesoRequest")
            .Should().Contain(["nombre", "areaSuperiorId", "companiaPrincipalId", "estado"]);
    }

    [Fact]
    public void La_actualizacion_no_permite_cambiar_la_jerarquia_ni_la_compania()
    {
        // Aceptar aquí areaSuperiorId o companiaPrincipalId permitiría reubicar el área —o cambiar
        // su Principal propietaria— esquivando las validaciones de /mover (RF-038, RF-046).
        fixture.DocumentoApi.PropiedadesDeEsquema("AreaAccesoUpdateRequest")
            .Should().BeEquivalentTo(["nombre", "estado"]);
    }

    [Fact]
    public void El_area_expone_su_compania_principal_como_dato_propio()
    {
        // Contraste deliberado con UnidadOrganizativa: aquí la pertenencia es una FK del propio nodo
        // (RF-046), no un valor resuelto recorriendo hasta la raíz (RF-044).
        var propiedad = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("AreaAccesoDto")
            .GetProperty("properties").GetProperty("companiaPrincipalId");

        // No es anulable: toda área pertenece a exactamente una Principal.
        propiedad.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void El_nodo_del_arbol_es_recursivo()
    {
        var hijos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("NodoAreaDto")
            .GetProperty("properties").GetProperty("hijos");

        hijos.GetProperty("type").GetString().Should().Be("array");
        hijos.GetProperty("items").GetProperty("$ref").GetString().Should().EndWith("NodoAreaDto");
    }

    [Fact]
    public void Ninguna_operacion_del_arbol_es_anonima()
    {
        foreach (var (ruta, metodo) in OperacionesUs6)
        {
            var operacion = fixture.DocumentoApi.Operacion(ruta, metodo);

            if (operacion.TryGetProperty("security", out var seguridad))
            {
                seguridad.GetArrayLength().Should().BeGreaterThan(
                    0, "{0} {1} no debe quedar anónima", metodo, ruta);
            }
        }
    }
}
