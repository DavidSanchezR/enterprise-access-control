using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad de los endpoints <c>tipos-persona</c> de <c>contracts/area-access.yaml</c>
/// (Historia 7, RF-019).
/// </summary>
/// <remarks>
/// Complemento de <see cref="AreasAccesoContractTests"/>, que cubre las operaciones del árbol
/// (Historia 6). Se separan porque son historias distintas y cada una debe poder verificarse sin la
/// otra.
/// </remarks>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class AreaAccesoTipoPersonaContractTests(ApiContratoFixture fixture)
{
    private const string Ruta = "/api/areas-acceso/{id}/tipos-persona";

    private static readonly ContratoOpenApi Contrato =
        ApiContratoFixture.CargarContrato("area-access.yaml");

    [Fact]
    public void El_contrato_declara_las_dos_operaciones_de_tipos_persona()
    {
        Contrato.Operaciones.Should().Contain([(Ruta, "get"), (Ruta, "put")]);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("put")]
    public void La_api_expone_la_operacion_del_contrato(string metodo)
    {
        fixture.DocumentoApi.TieneOperacion(Ruta, metodo).Should().BeTrue();
    }

    [Theory]
    [InlineData("get")]
    [InlineData("put")]
    public void La_api_declara_los_codigos_de_estado_del_contrato(string metodo)
    {
        var esperados = Contrato.Estados[(Ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(Ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void La_lectura_devuelve_una_lista_de_identificadores()
    {
        // El contrato devuelve identificadores desnudos, no objetos: la pantalla ya conoce el
        // catálogo de tipos de persona y solo necesita saber cuáles están marcados.
        var esquema = fixture.DocumentoApi.Operacion(Ruta, "get")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");

        esquema.GetProperty("type").GetString().Should().Be("array");
        esquema.GetProperty("items").GetProperty("format").GetString().Should().Be("uuid");
    }

    [Fact]
    public void El_reemplazo_exige_el_conjunto_completo_de_tipos()
    {
        // contracts/area-access.yaml: required: [tipoPersonaIds]. La operación reemplaza el conjunto
        // entero, así que omitir el campo no puede interpretarse como "déjalo como está".
        var esquema = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("ReemplazarTiposPersonaRequest");

        esquema.GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(["tipoPersonaIds"]);

        esquema.GetProperty("required").EnumerateArray().Select(v => v.GetString())
            .Should().BeEquivalentTo(["tipoPersonaIds"]);
    }

    [Fact]
    public void El_reemplazo_recibe_identificadores_y_no_nombres()
    {
        var items = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("ReemplazarTiposPersonaRequest")
            .GetProperty("properties").GetProperty("tipoPersonaIds");

        items.GetProperty("type").GetString().Should().Be("array");
        items.GetProperty("items").GetProperty("format").GetString().Should().Be("uuid");
    }

    [Fact]
    public void El_cuerpo_del_reemplazo_es_obligatorio()
    {
        fixture.DocumentoApi.Operacion(Ruta, "put")
            .GetProperty("requestBody").GetProperty("required").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public void Ninguna_operacion_de_tipos_persona_es_anonima()
    {
        foreach (var metodo in new[] { "get", "put" })
        {
            var operacion = fixture.DocumentoApi.Operacion(Ruta, metodo);

            if (operacion.TryGetProperty("security", out var seguridad))
            {
                seguridad.GetArrayLength().Should().BeGreaterThan(
                    0, "{0} {1} no debe quedar anónima", metodo, Ruta);
            }
        }
    }
}
