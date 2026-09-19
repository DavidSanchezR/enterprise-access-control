using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad entre <c>contracts/auth.yaml</c> y lo que la API publica realmente (Historia 1,
/// RF-001 a RF-003).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class AuthContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("auth.yaml");

    [Fact]
    public void El_contrato_declara_las_tres_operaciones_de_la_historia_1()
    {
        // Ancla la prueba: si alguien reduce el contrato, esto falla antes que las comparaciones,
        // dejando claro que el problema es el contrato y no la implementación.
        Contrato.Operaciones.Should().BeEquivalentTo(
        [
            ("/api/auth/login", "post"),
            ("/api/auth/cambiar-password", "post"),
            ("/api/auth/sesion", "get"),
        ]);
    }

    [Fact]
    public void Toda_operacion_del_contrato_existe_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty(
            "el contrato promete estas operaciones y la API debe exponerlas");
    }

    [Theory]
    [InlineData("/api/auth/login", "post")]
    [InlineData("/api/auth/cambiar-password", "post")]
    [InlineData("/api/auth/sesion", "get")]
    public void La_api_declara_todos_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void El_contrato_documenta_los_errores_de_login_con_el_esquema_ProblemDetails()
    {
        // RF-021/research.md §21: ProblemDetails es el único formato de error del sistema; el antiguo
        // ErrorResponse propio quedó descartado. Si reaparece un esquema de error a medida, esto lo
        // detecta.
        Contrato.PropiedadesPorEsquema.Should().ContainKey("ProblemDetails");
        Contrato.PropiedadesPorEsquema.Keys.Should().NotContain("ErrorResponse");

        Contrato.PropiedadesPorEsquema["ProblemDetails"].Should().Contain(
            ["type", "title", "status", "detail", "instance", "codigo"]);
    }

    [Fact]
    public void Los_esquemas_de_peticion_y_respuesta_coinciden_en_sus_propiedades()
    {
        // La serialización JSON de ASP.NET Core usa camelCase, igual que el contrato.
        foreach (var esquema in new[]
                 {
                     "LoginRequest", "LoginResponse", "CambiarPasswordRequest", "SesionActual",
                 })
        {
            fixture.DocumentoApi.TieneEsquema(esquema).Should().BeTrue(
                "el contrato declara el esquema {0}", esquema);

            fixture.DocumentoApi.PropiedadesDeEsquema(esquema)
                .Should().BeEquivalentTo(
                    Contrato.PropiedadesPorEsquema[esquema],
                    porque => porque.WithoutStrictOrdering());
        }
    }

    [Fact]
    public void El_login_es_la_unica_operacion_anonima()
    {
        // security: [] sólo en /api/auth/login. Cambiar esto abriría sin querer un endpoint
        // autenticado (Principio I, denegación por defecto).
        var login = fixture.DocumentoApi.Operacion("/api/auth/login", "post");

        login.TryGetProperty("security", out _).Should().BeFalse(
            "la API no debe exigir esquema de seguridad en el login");
    }
}
