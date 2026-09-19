using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad del <c>CredencialesController</c> con <c>contracts/credentials.yaml</c> (Historia 9, T154).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class CredencialesApiContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato =
        ApiContratoFixture.CargarContrato("credentials.yaml");

    [Fact]
    public void Todas_las_operaciones_del_contrato_existen_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/personas/{personaId}/credenciales", "get")]
    [InlineData("/api/personas/{personaId}/credenciales", "post")]
    [InlineData("/api/personas/{personaId}/credenciales/{id}/devolver", "post")]
    [InlineData("/api/personas/{personaId}/credenciales/{id}", "delete")]
    public void La_api_declara_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        fixture.DocumentoApi.EstadosDeclarados(ruta, metodo)
            .Should().Contain(Contrato.Estados[(ruta, metodo)]);
    }

    [Fact]
    public void Los_esquemas_de_credencial_coinciden_en_sus_propiedades()
    {
        foreach (var (enContrato, enApi) in new Dictionary<string, string>(StringComparer.Ordinal)
                 {
                     ["AsignacionCredencial"] = "AsignacionCredencialDto",
                     ["AsignacionCredencialRequest"] = "AsignacionCredencialRequest",
                 })
        {
            fixture.DocumentoApi.PropiedadesDeEsquema(enApi)
                .Should().BeEquivalentTo(
                    Contrato.PropiedadesPorEsquema[enContrato],
                    porque => porque.WithoutStrictOrdering(),
                    "el esquema {0} debe tener la misma forma que {1}", enContrato, enApi);
        }
    }

    [Fact]
    public void El_alta_exige_los_cuatro_campos_del_contrato()
    {
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("AsignacionCredencialRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().BeEquivalentTo(
            ["companiaPrincipalId", "tipoCredencialId", "fechaHoraInicio", "fechaHoraFin"]);
    }

    [Fact]
    public void El_listado_admite_filtrar_por_compania_principal()
    {
        var parametros = fixture.DocumentoApi.Operacion("/api/personas/{personaId}/credenciales", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        parametros.Should().Contain("companiaPrincipalId");
    }

    [Fact]
    public void Ninguna_operacion_de_credenciales_es_anonima()
    {
        foreach (var (ruta, metodo) in Contrato.Operaciones)
        {
            var operacion = fixture.DocumentoApi.Operacion(ruta, metodo);

            if (operacion.TryGetProperty("security", out var seguridad))
            {
                seguridad.GetArrayLength().Should().BeGreaterThan(0);
            }
        }
    }
}
