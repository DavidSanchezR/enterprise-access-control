using System.Text.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Auth.Validators;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad entre <c>contracts/users.yaml</c> y lo que la API publica realmente (RF-004, RF-005).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class UsersContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("users.yaml");

    /// <summary>
    /// Nombre del esquema en el contrato → nombre del tipo que la API genera.
    /// </summary>
    /// <remarks>
    /// El contrato nombra los esquemas desde el punto de vista del consumidor (<c>Usuario</c>,
    /// <c>PaginaUsuarios</c>); la API los nombra por el tipo .NET que serializa. El mapeo se declara
    /// explícitamente para que una divergencia de forma se detecte aunque los nombres difieran, en
    /// lugar de renombrar los DTO para que coincidan por casualidad.
    /// </remarks>
    private static readonly Dictionary<string, string> EsquemasEquivalentes = new(StringComparer.Ordinal)
    {
        ["Usuario"] = "UsuarioDto",
        ["PaginaUsuarios"] = "PaginaResponseOfUsuarioDto",
        ["CrearUsuarioRequest"] = "CrearUsuarioRequest",
        ["ActualizarUsuarioRequest"] = "ActualizarUsuarioRequest",
    };

    [Fact]
    public void Toda_operacion_del_contrato_existe_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty(
            "el contrato promete estas operaciones y la API debe exponerlas");
    }

    [Fact]
    public void La_api_no_expone_operaciones_de_usuarios_fuera_del_contrato()
    {
        // Superficie mínima: un endpoint de usuarios no documentado es un endpoint que nadie revisó
        // contra las reglas de alcance (Principio I).
        var enLaApi = fixture.DocumentoApi.Paths()
            .EnumerateObject()
            .Where(p => p.Name.StartsWith("/api/usuarios", StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Select(m => (Ruta: p.Name, Metodo: m.Name)))
            .ToList();

        enLaApi.Should().BeEquivalentTo(Contrato.Operaciones);
    }

    [Theory]
    [InlineData("/api/usuarios", "get")]
    [InlineData("/api/usuarios", "post")]
    [InlineData("/api/usuarios/{id}", "get")]
    [InlineData("/api/usuarios/{id}", "put")]
    [InlineData("/api/usuarios/{id}/desbloquear", "post")]
    [InlineData("/api/usuarios/{id}/alcance-companias", "get")]
    [InlineData("/api/usuarios/{id}/alcance-companias", "put")]
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
                    "el esquema {0} del contrato debe tener la misma forma que {1}", enContrato, enApi);
        }
    }

    [Fact]
    public void El_enumerado_EstadoUsuario_expone_exactamente_los_valores_del_contrato()
    {
        // Los valores viajan como literales de texto (nvarchar en la base, string en el JSON): un
        // valor nuevo o renombrado rompería tanto el contrato como los datos ya persistidos.
        var enApi = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("EstadoUsuario")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        enApi.Should().BeEquivalentTo(["ACTIVO", "INACTIVO", "BLOQUEADO"]);
    }

    [Fact]
    public void La_paginacion_usa_los_nombres_de_parametro_del_contrato()
    {
        // "tamañoPagina" lleva eñe en el contrato; el binding debe respetarlo tal cual.
        var parametros = fixture.DocumentoApi.Operacion("/api/usuarios", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        parametros.Should().Contain(["pagina", "tamañoPagina", "estado"]);
    }

    [Fact]
    public void Los_errores_se_documentan_con_ProblemDetails_y_no_con_un_esquema_propio()
    {
        // research.md §21: ProblemDetails RFC 7807/9457 es el único formato de error del sistema.
        Contrato.PropiedadesPorEsquema.Should().ContainKey("ProblemDetails");
        Contrato.PropiedadesPorEsquema.Keys.Should().NotContain("ErrorResponse");

        // El documento generado declara ProblemDetails con sus campos estándar. La extensión propia
        // "codigo" no aparece en el esquema estático porque viaja en Extensions; que esté presente en
        // la respuesta real se verifica en las pruebas de integración, contra un error auténtico.
        fixture.DocumentoApi.PropiedadesDeEsquema("ProblemDetails")
            .Should().Contain(["type", "title", "status", "detail", "instance"]);
    }

    [Fact]
    public void El_alta_de_usuario_exige_al_menos_una_compania_en_el_alcance()
    {
        // minItems: 1 en el contrato — un usuario sin alcance no podría administrar nada (RF-005).
        // Se verifica sobre el validador y no sólo sobre el documento, porque es ahí donde la
        // restricción se impone de verdad: un contrato que la declare y una API que la ignore sería
        // exactamente la divergencia que estas pruebas existen para detectar.
        var validador = new CrearUsuarioRequestValidator(
            new PasswordPolicyValidator(Options.Create(new PasswordPolicyOptions())));

        var sinCompanias = new CrearUsuarioRequest("admin@empresa.cl", "Contrasena1Valida", []);

        validador.Validate(sinCompanias).IsValid.Should().BeFalse(
            "el contrato declara minItems: 1 para companiaIds");

        var conCompania = new CrearUsuarioRequest(
            "admin@empresa.cl",
            "Contrasena1Valida",
            [Guid.CreateVersion7()]);

        validador.Validate(conCompania).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Todas_las_operaciones_de_usuarios_requieren_autenticacion()
    {
        // No hay "security: []" en users.yaml: ninguna operación de mantenimiento es anónima.
        foreach (var (ruta, metodo) in Contrato.Operaciones)
        {
            var operacion = fixture.DocumentoApi.Operacion(ruta, metodo);

            if (operacion.TryGetProperty("security", out var seguridad))
            {
                seguridad.ValueKind.Should().Be(JsonValueKind.Array);
                seguridad.GetArrayLength().Should().BeGreaterThan(
                    0, "{0} {1} no debe quedar anónima", metodo, ruta);
            }
        }
    }
}
