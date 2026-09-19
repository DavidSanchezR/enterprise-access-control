using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad entre <c>contracts/masters.yaml</c> y lo que la API publica para los cinco catálogos
/// (Historia 3, RF-030 a RF-032).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class MaestrosContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("masters.yaml");

    /// <summary>Los cinco grupos de rutas que declara el contrato.</summary>
    private static readonly string[] Catalogos =
        ["tipos-documento", "tipos-sangre", "generos", "tipos-persona", "tipos-credencial"];

    [Fact]
    public void El_contrato_declara_los_cinco_catalogos_con_sus_tres_operaciones()
    {
        var esperadas = Catalogos
            .SelectMany(catalogo => new[]
            {
                ($"/api/maestros/{catalogo}", "get"),
                ($"/api/maestros/{catalogo}", "post"),
                ($"/api/maestros/{catalogo}/{{id}}", "put"),
            })
            .ToList();

        Contrato.Operaciones.Should().BeEquivalentTo(esperadas);
    }

    [Fact]
    public void Toda_operacion_del_contrato_existe_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Fact]
    public void La_api_no_expone_operaciones_de_maestros_fuera_del_contrato()
    {
        var enLaApi = fixture.DocumentoApi.Paths()
            .EnumerateObject()
            .Where(p => p.Name.StartsWith("/api/maestros", StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Select(m => (Ruta: p.Name, Metodo: m.Name)))
            .ToList();

        enLaApi.Should().BeEquivalentTo(Contrato.Operaciones);
    }

    [Theory]
    [InlineData("tipos-documento")]
    [InlineData("tipos-sangre")]
    [InlineData("generos")]
    [InlineData("tipos-persona")]
    [InlineData("tipos-credencial")]
    public void Cada_catalogo_declara_los_codigos_de_estado_de_su_contrato(string catalogo)
    {
        foreach (var (ruta, metodo) in new[]
                 {
                     ($"/api/maestros/{catalogo}", "get"),
                     ($"/api/maestros/{catalogo}", "post"),
                     ($"/api/maestros/{catalogo}/{{id}}", "put"),
                 })
        {
            var esperados = Contrato.Estados[(ruta, metodo)];
            var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

            declarados.Should().Contain(esperados, "en {0} {1}", metodo, ruta);
        }
    }

    [Theory]
    [InlineData("tipos-documento")]
    [InlineData("tipos-sangre")]
    [InlineData("generos")]
    [InlineData("tipos-persona")]
    [InlineData("tipos-credencial")]
    public void Cada_catalogo_admite_el_filtro_por_estado(string catalogo)
    {
        var parametros = fixture.DocumentoApi.Operacion($"/api/maestros/{catalogo}", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        // RF-032: un valor INACTIVO sigue siendo legible; el filtro permite ofrecer solo los activos
        // en los selectores de asignación.
        parametros.Should().Contain("estado");
    }

    [Fact]
    public void Los_cinco_catalogos_comparten_la_misma_forma_de_item()
    {
        // Que compartan forma es lo que justifica un único servicio genérico: si un catálogo
        // divergiera, esta prueba lo detectaría antes de que la generalización dejara de ser válida.
        fixture.DocumentoApi.PropiedadesDeEsquema("MasterItem")
            .Should().BeEquivalentTo(Contrato.PropiedadesPorEsquema["MasterItem"]);

        fixture.DocumentoApi.PropiedadesDeEsquema("MasterItemRequest")
            .Should().BeEquivalentTo(Contrato.PropiedadesPorEsquema["MasterItemRequest"]);
    }

    [Theory]
    [InlineData("tipos-documento")]
    [InlineData("tipos-sangre")]
    [InlineData("generos")]
    [InlineData("tipos-persona")]
    [InlineData("tipos-credencial")]
    public void Todos_los_catalogos_usan_el_mismo_esquema_de_item_en_la_respuesta(string catalogo)
    {
        var esquema = fixture.DocumentoApi.Operacion($"/api/maestros/{catalogo}", "get")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");

        esquema.GetProperty("type").GetString().Should().Be("array");
        esquema.GetProperty("items").GetProperty("$ref").GetString().Should().EndWith("MasterItem");
    }

    [Fact]
    public void El_estado_del_catalogo_expone_exactamente_ACTIVO_e_INACTIVO()
    {
        var valores = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("Estado")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        valores.Should().BeEquivalentTo(["ACTIVO", "INACTIVO"]);
    }

    [Fact]
    public void Ningun_catalogo_es_anonimo()
    {
        foreach (var (ruta, metodo) in Contrato.Operaciones)
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
