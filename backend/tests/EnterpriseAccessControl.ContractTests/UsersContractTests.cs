using System.Text.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Auth.Validators;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.ContractTests.Infraestructura;
using EnterpriseAccessControl.Domain.Enums;
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
    [InlineData("/api/usuarios/{id}/roles", "get")]
    [InlineData("/api/usuarios/{id}/roles", "post")]
    [InlineData("/api/usuarios/{id}/roles/{asignacionId}/finalizar", "post")]
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
    public void El_listado_declara_el_parametro_de_busqueda_del_contrato()
    {
        // `OpenApiSnapshotTests` compara rutas, estados y cuerpos, no parámetros de consulta: sin esta
        // aserción, el contrato podría declarar `texto` (v2.1.0, UX-22) y la API no implementarlo sin
        // que ninguna prueba lo notara — exactamente la divergencia que D-4 vino a cerrar.
        var enElContrato = Contrato.ParametrosDeConsulta("/api/usuarios", "get");

        enElContrato.Should().Contain("texto");

        var enLaApi = fixture.DocumentoApi.Operacion("/api/usuarios", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        enLaApi.Should().Contain("texto");
    }

    [Fact]
    public void La_renovacion_de_asignacion_existe_y_acepta_fechaHoraFin()
    {
        // Cierre de D-1: la ruta estuvo declarada en el contrato sin implementación. El nombre del
        // campo importa: el contrato de renovación de personas usa `fechaHoraFin`, y un `nuevaFechaHoraFin`
        // obligaría al cliente a recordar dos nombres para la misma operación.
        const string ruta = "/api/usuarios/{id}/roles/{asignacionId}/renovar";

        fixture.DocumentoApi.TieneOperacion(ruta, "post").Should().BeTrue();

        fixture.DocumentoApi.EstadosDeclarados(ruta, "post")
            .Should().Contain(["204", "400", "403", "404", "409"]);

        var propiedades = fixture.DocumentoApi.PropiedadesDeEsquema(
            nameof(RenovarAsignacionRolRequest));

        propiedades.Should().BeEquivalentTo(["fechaHoraFin"]);
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
    public void El_alta_de_usuario_impone_la_regla_fundamental_de_rol_y_compania()
    {
        // RF-074: CompañíaId debe ser NULL para GLOBAL_ADMINISTRATOR y obligatoria para
        // COMPANY_ADMINISTRATOR. Se verifica sobre el validador y no sólo sobre el documento, porque
        // es ahí donde la restricción se impone de verdad: un contrato que la declare y una API que la
        // ignore sería exactamente la divergencia que estas pruebas existen para detectar.
        var validador = new CrearUsuarioRequestValidator(
            new PasswordPolicyValidator(Options.Create(new PasswordPolicyOptions())));

        var inicio = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddYears(1);

        var globalConCompania = new CrearUsuarioRequest(
            "admin@empresa.cl",
            "Contrasena1Valida",
            RolAdministrativo.GLOBAL_ADMINISTRATOR,
            Guid.CreateVersion7(),
            inicio,
            fin);

        validador.Validate(globalConCompania).IsValid.Should().BeFalse(
            "un GLOBAL_ADMINISTRATOR no admite compañía");

        var companySinCompania = new CrearUsuarioRequest(
            "admin@empresa.cl",
            "Contrasena1Valida",
            RolAdministrativo.COMPANY_ADMINISTRATOR,
            null,
            inicio,
            fin);

        validador.Validate(companySinCompania).IsValid.Should().BeFalse(
            "un COMPANY_ADMINISTRATOR exige una compañía");

        var global = new CrearUsuarioRequest(
            "admin@empresa.cl",
            "Contrasena1Valida",
            RolAdministrativo.GLOBAL_ADMINISTRATOR,
            null,
            inicio,
            fin);

        validador.Validate(global).IsValid.Should().BeTrue();

        var porCompania = new CrearUsuarioRequest(
            "admin@empresa.cl",
            "Contrasena1Valida",
            RolAdministrativo.COMPANY_ADMINISTRATOR,
            Guid.CreateVersion7(),
            inicio,
            fin);

        validador.Validate(porCompania).IsValid.Should().BeTrue();
    }

    [Fact]
    public void La_vigencia_de_la_primera_asignacion_es_obligatoria_y_real()
    {
        // RF-075: nunca null ni fecha centinela, y el fin debe ser posterior al inicio.
        var validador = new CrearUsuarioRequestValidator(
            new PasswordPolicyValidator(Options.Create(new PasswordPolicyOptions())));

        var inicio = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

        var invertida = new CrearUsuarioRequest(
            "admin@empresa.cl",
            "Contrasena1Valida",
            RolAdministrativo.GLOBAL_ADMINISTRATOR,
            null,
            inicio,
            inicio.AddDays(-1));

        validador.Validate(invertida).IsValid.Should().BeFalse();
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
