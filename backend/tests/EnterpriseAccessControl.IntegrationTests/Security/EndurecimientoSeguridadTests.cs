using System.Net;
using EnterpriseAccessControl.Infrastructure.Security;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.Security;

/// <summary>
/// Revisión de endurecimiento: todo endpoint protegido exige autenticación y, los de datos de
/// compañía, la política <c>CompaniaScope</c> (T163; CS-004, RF-049, RF-060; Principio I).
/// </summary>
/// <remarks>
/// La revisión se hace sobre los endpoints realmente registrados en la aplicación
/// (<see cref="EndpointDataSource"/>), no sobre una lista escrita a mano: un controlador nuevo entra
/// en la verificación sin que nadie tenga que acordarse de añadirlo.
///
/// Excepciones deliberadas, y únicas:
/// <list type="bullet">
///   <item>Anónimos: <c>POST /api/auth/login</c>, <c>/health/live</c>, <c>/health/ready</c> y el
///   documento OpenAPI (solo en Development).</item>
///   <item>Autenticados sin <c>CompaniaScope</c>: <c>cambiar-password</c> y <c>sesion</c>. Operan sobre
///   la cuenta del propio usuario, no sobre datos de una compañía; exigir alcance impediría a un usuario
///   sin compañías asignadas cambiar su contraseña o consultar su sesión.</item>
/// </list>
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class EndurecimientoSeguridadTests(SqlServerFixture fixture)
{
    private static readonly string[] Anonimos =
    [
        "POST api/auth/login",
        "GET /health/live",
        "GET /health/ready",
        "GET /openapi/{documentName}.json",
    ];

    private static readonly string[] SoloAutenticados =
    [
        "POST api/auth/cambiar-password",
        "GET api/auth/sesion",
    ];

    private sealed record Punto(string Clave, string Metodo, string Plantilla, EndpointMetadataCollection Metadatos);

    [Fact]
    public void Solo_los_endpoints_previstos_admiten_acceso_anonimo()
    {
        var anonimos = Puntos()
            .Where(p => p.Metadatos.GetMetadata<IAllowAnonymous>() is not null)
            .Select(p => p.Clave)
            .ToList();

        anonimos.Should().BeEquivalentTo(Anonimos);
    }

    [Fact]
    public void Todo_endpoint_de_datos_de_compania_exige_la_politica_CompaniaScope()
    {
        var sinPolitica = Puntos()
            .Where(p => !Anonimos.Contains(p.Clave) && !SoloAutenticados.Contains(p.Clave))
            .Where(p => !p.Metadatos.GetOrderedMetadata<IAuthorizeData>()
                .Any(a => a.Policy == CompaniaScopeRequirement.PolicyName))
            .Select(p => p.Clave)
            .ToList();

        sinPolitica.Should().BeEmpty();
    }

    [Fact]
    public void Los_endpoints_de_cuenta_propia_exigen_al_menos_autenticacion()
    {
        foreach (var clave in SoloAutenticados)
        {
            var punto = Puntos().Single(p => p.Clave == clave);

            punto.Metadatos.GetOrderedMetadata<IAuthorizeData>().Should().NotBeEmpty(clave);
            punto.Metadatos.GetMetadata<IAllowAnonymous>().Should().BeNull(clave);
        }
    }

    [Fact]
    public void Los_endpoints_mencionados_en_la_revision_estan_cubiertos()
    {
        // T163 cita expresamente estos grupos; se comprueba que existen y que la regla anterior los alcanzó.
        var claves = Puntos().Select(p => p.Clave).ToList();

        foreach (var fragmento in new[]
                 {
                     "api/unidades-organizativas",
                     "api/areas-acceso",
                     "relaciones-principales",
                     "contextos-operativos",
                     "/renovar",
                 })
        {
            claves.Should().Contain(c => c.Contains(fragmento, StringComparison.Ordinal), fragmento);
        }
    }

    [Fact]
    public async Task Sin_token_todo_endpoint_protegido_responde_401()
    {
        using var anonimo = fixture.Api.CrearCliente();
        var fallos = new List<string>();

        foreach (var punto in Puntos().Where(p => !Anonimos.Contains(p.Clave)))
        {
            using var peticion = new HttpRequestMessage(new HttpMethod(punto.Metodo), RutaConcreta(punto.Plantilla));

            if (punto.Metodo is "POST" or "PUT")
            {
                peticion.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            }

            using var respuesta = await anonimo.SendAsync(peticion);

            if (respuesta.StatusCode != HttpStatusCode.Unauthorized)
            {
                fallos.Add($"{punto.Clave} → {(int)respuesta.StatusCode}");
            }
        }

        fallos.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_usuario_autenticado_sin_alcance_recibe_403_en_los_datos_de_compania_pero_accede_a_su_sesion()
    {
        var sinAlcance = await fixture.Api.SembrarUsuarioAsync(
            $"sin.alcance.{Guid.CreateVersion7():N}@empresa.cl", "Contrasena1Segura");

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(sinAlcance.Id, sinAlcance.Correo);

        using (var companias = await cliente.GetAsync(new Uri("/api/companias", UriKind.Relative)))
        {
            companias.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using (var sesion = await cliente.GetAsync(new Uri("/api/auth/sesion", UriKind.Relative)))
        {
            sesion.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Los_endpoints_anonimos_responden_sin_token()
    {
        using var anonimo = fixture.Api.CrearCliente();

        using (var vivo = await anonimo.GetAsync(new Uri("/health/live", UriKind.Relative)))
        {
            vivo.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var listo = await anonimo.GetAsync(new Uri("/health/ready", UriKind.Relative)))
        {
            listo.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var login = await anonimo.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new StringContent("""{"correo":"nadie@empresa.cl","password":"x"}""", System.Text.Encoding.UTF8, "application/json"));

        // Con credenciales inválidas el login también responde 401, así que el código no basta para saber
        // si llegó al caso de uso. El ProblemDetails lo emite el caso de uso; el rechazo de la política
        // de respaldo no lleva cuerpo.
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        login.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    // --- Utilidades --------------------------------------------------------------------------------

    private IEnumerable<Punto> Puntos()
    {
        using var ambito = fixture.Api.Services.CreateScope();
        var fuente = ambito.ServiceProvider.GetRequiredService<EndpointDataSource>();

        foreach (var endpoint in fuente.Endpoints.OfType<RouteEndpoint>())
        {
            var metodos = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"];

            foreach (var metodo in metodos)
            {
                var plantilla = endpoint.RoutePattern.RawText ?? string.Empty;
                yield return new Punto($"{metodo} {plantilla}", metodo, plantilla, endpoint.Metadata);
            }
        }
    }

    /// <summary>Sustituye los parámetros de ruta por valores válidos para su restricción.</summary>
    private static Uri RutaConcreta(string plantilla)
    {
        var ruta = System.Text.RegularExpressions.Regex.Replace(
            plantilla,
            @"\{([^}:]+)(:[^}]+)?\}",
            m => m.Groups[1].Value == "catalogo" ? "tipos-documento" : Guid.CreateVersion7().ToString());

        return new Uri("/" + ruta.TrimStart('/'), UriKind.Relative);
    }
}
