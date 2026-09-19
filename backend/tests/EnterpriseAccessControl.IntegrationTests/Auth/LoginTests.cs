using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// Historia 1, criterios 1–3: login exitoso, rechazo de usuarios no habilitados y cambio forzado de
/// contraseña (RF-001, RF-002, RF-003), contra SQL Server real.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class LoginTests(SqlServerFixture fixture)
{
    private const string PasswordValida = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private static Task<HttpResponseMessage> LoginAsync(HttpClient cliente, string correo, string password) =>
        cliente.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest(correo, password));

    [Fact]
    public async Task Un_usuario_activo_con_credenciales_correctas_obtiene_token_y_su_alcance()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Minera Norte");
        var correo = CorreoUnico("activo");

        await fixture.Api.SembrarUsuarioAsync(correo, PasswordValida, alcanceCompanias: [compania.Id]);

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo, PasswordValida);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json);

        cuerpo.Should().NotBeNull();
        cuerpo!.AccessToken.Should().NotBeNullOrWhiteSpace();
        cuerpo.ExpiraEn.Should().BeAfter(DateTime.UtcNow);
        cuerpo.RequiereCambioPassword.Should().BeFalse();
        cuerpo.AlcanceCompanias.Should().ContainSingle().Which.Should().Be(compania.Id);
    }

    [Fact]
    public async Task Un_usuario_inactivo_no_puede_iniciar_sesion()
    {
        var correo = CorreoUnico("inactivo");
        await fixture.Api.SembrarUsuarioAsync(correo, PasswordValida, EstadoUsuario.INACTIVO);

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo, PasswordValida);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema.Should().NotBeNull();
        problema!.Extensions.Should().ContainKey("codigo");
    }

    [Fact]
    public async Task Un_usuario_bloqueado_no_puede_iniciar_sesion_ni_con_la_contrasena_correcta()
    {
        var correo = CorreoUnico("bloqueado");
        await fixture.Api.SembrarUsuarioAsync(correo, PasswordValida, EstadoUsuario.BLOQUEADO);

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo, PasswordValida);

        // 403 y no 401: la contraseña era correcta; lo que impide entrar es el estado de la cuenta.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Una_contrasena_incorrecta_devuelve_401_con_ProblemDetails()
    {
        var correo = CorreoUnico("malpass");
        await fixture.Api.SembrarUsuarioAsync(correo, PasswordValida);

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo, "OtraContrasena9");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Status.Should().Be(401);
        problema.Extensions["codigo"]!.ToString().Should().Be("CREDENCIALES_INVALIDAS");
    }

    [Fact]
    public async Task Un_correo_inexistente_devuelve_el_mismo_error_que_una_contrasena_incorrecta()
    {
        var correoConocido = CorreoUnico("existe");
        await fixture.Api.SembrarUsuarioAsync(correoConocido, PasswordValida);

        using var cliente = fixture.Api.CrearCliente();

        using var inexistente = await LoginAsync(cliente, CorreoUnico("fantasma"), PasswordValida);
        using var passwordMala = await LoginAsync(cliente, correoConocido, "OtraContrasena9");

        // Respuestas indistinguibles: cualquier diferencia permitiría enumerar correos registrados.
        inexistente.StatusCode.Should().Be(passwordMala.StatusCode);

        var unProblema = await inexistente.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        var otroProblema = await passwordMala.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);

        unProblema!.Detail.Should().Be(otroProblema!.Detail);
        unProblema.Extensions["codigo"]!.ToString().Should()
            .Be(otroProblema.Extensions["codigo"]!.ToString());
    }

    [Fact]
    public async Task El_login_no_distingue_mayusculas_y_minusculas_en_el_correo()
    {
        var correo = CorreoUnico("Mayusculas");
        await fixture.Api.SembrarUsuarioAsync(correo, PasswordValida);

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo.ToUpperInvariant(), PasswordValida);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_usuario_marcado_para_cambio_de_contrasena_lo_recibe_en_la_respuesta()
    {
        var correo = CorreoUnico("cambio");
        await fixture.Api.SembrarUsuarioAsync(correo, PasswordValida, requiereCambioPassword: true);

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo, PasswordValida);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json);
        cuerpo!.RequiereCambioPassword.Should().BeTrue(
            "el token se emite igualmente, pero el cliente debe forzar el cambio (Historia 1, criterio 3)");
    }

    [Fact]
    public async Task Una_contrasena_expirada_obliga_a_cambiarla_sin_impedir_el_acceso()
    {
        var correo = CorreoUnico("expirada");

        // DiasExpiracion por defecto es 90: se sitúa el último cambio claramente fuera de esa ventana.
        await fixture.Api.SembrarUsuarioAsync(
            correo,
            PasswordValida,
            fechaUltimoCambioPassword: DateTime.UtcNow.AddDays(-400));

        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await LoginAsync(cliente, correo, PasswordValida);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json);
        cuerpo!.RequiereCambioPassword.Should().BeTrue();
    }

    [Fact]
    public async Task El_token_emitido_da_acceso_a_la_sesion_y_refleja_el_alcance()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Contratista Sur", TipoCompania.CONTRATISTA);
        var correo = CorreoUnico("sesion");

        var usuario = await fixture.Api.SembrarUsuarioAsync(
            correo,
            PasswordValida,
            alcanceCompanias: [compania.Id]);

        using var cliente = fixture.Api.CrearCliente();
        using var login = await LoginAsync(cliente, correo, PasswordValida);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json))!.AccessToken;

        using var autenticado = fixture.Api.CrearCliente();
        autenticado.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var sesion = await autenticado.GetAsync(new Uri("/api/auth/sesion", UriKind.Relative));

        sesion.StatusCode.Should().Be(HttpStatusCode.OK);

        var actual = await sesion.Content.ReadFromJsonAsync<SesionActual>(ApiFactory.Json);
        actual!.UsuarioId.Should().Be(usuario.Id);
        actual.Correo.Should().Be(correo);
        actual.AlcanceCompanias.Should().ContainSingle().Which.Should().Be(compania.Id);
    }

    [Fact]
    public async Task La_sesion_sin_token_se_rechaza()
    {
        using var cliente = fixture.Api.CrearCliente();
        using var respuesta = await cliente.GetAsync(new Uri("/api/auth/sesion", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
