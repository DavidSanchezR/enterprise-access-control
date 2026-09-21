using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// Bloqueo por intentos fallidos, expiración periódica y rechazo de reutilización mediante
/// <c>HistorialContraseña</c> (RF-002, RF-003; research.md §2), contra SQL Server real.
/// </summary>
/// <remarks>
/// Los umbrales se leen de la configuración efectiva de la API en lugar de codificarse: si negocio
/// cierra la Decisión Pendiente #1 con otros números, estas pruebas siguen siendo válidas.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PasswordPolicyTests(SqlServerFixture fixture)
{
    private const string PasswordInicial = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private PasswordPolicyOptions Politica =>
        fixture.Api.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<PasswordPolicyOptions>>()
            .Value;

    private static Task<HttpResponseMessage> LoginAsync(HttpClient cliente, string correo, string password) =>
        cliente.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest(correo, password));

    private static Task<HttpResponseMessage> CambiarAsync(
        HttpClient cliente,
        string actual,
        string nueva) =>
        cliente.PostAsJsonAsync(
            new Uri("/api/auth/cambiar-password", UriKind.Relative),
            new CambiarPasswordRequest(actual, nueva));

    [Fact]
    public async Task El_usuario_se_bloquea_tras_los_intentos_fallidos_configurados()
    {
        var umbral = Politica.IntentosFallidosParaBloqueo;
        var correo = CorreoUnico("bloqueo");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = fixture.Api.CrearCliente();

        // Los primeros (umbral - 1) fallos dejan la cuenta activa: aún son credenciales inválidas.
        for (var intento = 1; intento < umbral; intento++)
        {
            using var fallo = await LoginAsync(cliente, correo, "Incorrecta9X");
            fallo.StatusCode.Should().Be(
                HttpStatusCode.Unauthorized,
                "el intento {0} de {1} todavía no debe bloquear", intento, umbral);
        }

        using (var ultimoFallo = await LoginAsync(cliente, correo, "Incorrecta9X"))
        {
            // El intento que alcanza el umbral sigue respondiendo 401 —es un fallo de credenciales—
            // pero deja la cuenta BLOQUEADA.
            ultimoFallo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var persistido = await db.Set<Usuario>().AsNoTracking()
                .FirstAsync(u => u.Id == usuario.Id);

            persistido.Estado.Should().Be(EstadoUsuario.BLOQUEADO);
            persistido.IntentosFallidosConsecutivos.Should().BeGreaterThanOrEqualTo(umbral);
        });

        // Y a partir de ahí ni siquiera la contraseña correcta permite entrar.
        using var conCorrecta = await LoginAsync(cliente, correo, PasswordInicial);
        conCorrecta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_login_exitoso_reinicia_el_contador_de_intentos_fallidos()
    {
        var correo = CorreoUnico("reinicio");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = fixture.Api.CrearCliente();

        using (var fallo = await LoginAsync(cliente, correo, "Incorrecta9X"))
        {
            fallo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using (var exito = await LoginAsync(cliente, correo, PasswordInicial))
        {
            exito.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var persistido = await db.Set<Usuario>().AsNoTracking().FirstAsync(u => u.Id == usuario.Id);

            // Fallos aislados en el tiempo no deben acumularse hasta bloquear a un usuario legítimo.
            persistido.IntentosFallidosConsecutivos.Should().Be(0);
        });
    }

    [Fact]
    public async Task El_desbloqueo_administrativo_devuelve_el_usuario_a_ACTIVO()
    {
        var correo = CorreoUnico("desbloqueo");
        var usuario = await fixture.Api.SembrarUsuarioAsync(
            correo,
            PasswordInicial,
            EstadoUsuario.BLOQUEADO);

        // El usuario bloqueado no tiene ninguna asignación de rol, así que no pertenece al alcance de
        // ningún COMPANY_ADMINISTRATOR (RF-077). El desbloqueo administrativo lo ejecuta un
        // GLOBAL_ADMINISTRATOR, que sí administra a cualquier usuario (RF-074, RF-076).
        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"),
            PasswordInicial,
            global: true);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.PostAsync(
            new Uri($"/api/usuarios/{usuario.Id}/desbloquear", UriKind.Relative),
            content: null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Tras el desbloqueo la contraseña original vuelve a servir.
        using var anonimo = fixture.Api.CrearCliente();
        using var login = await LoginAsync(anonimo, correo, PasswordInicial);

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cambiar_la_contrasena_archiva_la_saliente_en_el_historial()
    {
        var correo = CorreoUnico("historial");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, correo);

        using var respuesta = await CambiarAsync(cliente, PasswordInicial, "NuevaContrasena2");
        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var historial = await db.Set<HistorialContrasena>().AsNoTracking()
                .Where(h => h.UsuarioId == usuario.Id)
                .ToListAsync();

            historial.Should().ContainSingle("la contraseña saliente debe quedar archivada");

            var persistido = await db.Set<Usuario>().AsNoTracking().FirstAsync(u => u.Id == usuario.Id);
            persistido.PasswordHash.Should().NotBe(historial[0].PasswordHash);
            persistido.RequiereCambioPassword.Should().BeFalse();
        });
    }

    [Fact]
    public async Task No_se_puede_reutilizar_la_contrasena_vigente()
    {
        var correo = CorreoUnico("misma");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, correo);
        using var respuesta = await CambiarAsync(cliente, PasswordInicial, PasswordInicial);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task No_se_puede_reutilizar_ninguna_de_las_ultimas_contrasenas_del_historial()
    {
        var correo = CorreoUnico("reutiliza");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, correo);

        // Se encadenan tantos cambios como contraseñas retiene la política, de modo que la primera
        // siga dentro de la ventana de no-reutilización.
        var retenidas = Politica.HistorialNoReutilizable;
        var actual = PasswordInicial;

        for (var i = 1; i <= retenidas - 1; i++)
        {
            var siguiente = $"Contrasena{i}Rotada";

            using var cambio = await CambiarAsync(cliente, actual, siguiente);
            cambio.StatusCode.Should().Be(HttpStatusCode.NoContent, "cambio {0} de {1}", i, retenidas - 1);

            actual = siguiente;
        }

        using var reintento = await CambiarAsync(cliente, actual, PasswordInicial);

        reintento.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await reintento.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("PASSWORD_REUTILIZADA");
    }

    [Fact]
    public async Task Una_contrasena_nueva_que_no_cumple_la_politica_se_rechaza_con_400()
    {
        var correo = CorreoUnico("debil");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, correo);
        using var respuesta = await CambiarAsync(cliente, PasswordInicial, "corta");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Cambiar_la_contrasena_con_la_actual_equivocada_se_rechaza()
    {
        var correo = CorreoUnico("actualmala");
        var usuario = await fixture.Api.SembrarUsuarioAsync(correo, PasswordInicial);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, correo);
        using var respuesta = await CambiarAsync(cliente, "NoEsLaActual9", "NuevaContrasena2");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task El_cambio_de_contrasena_limpia_la_marca_de_expiracion()
    {
        var correo = CorreoUnico("renueva");
        var usuario = await fixture.Api.SembrarUsuarioAsync(
            correo,
            PasswordInicial,
            fechaUltimoCambioPassword: DateTime.UtcNow.AddDays(-Politica.DiasExpiracion - 10));

        using var anonimo = fixture.Api.CrearCliente();

        using (var antes = await LoginAsync(anonimo, correo, PasswordInicial))
        {
            var cuerpo = await antes.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json);
            cuerpo!.RequiereCambioPassword.Should().BeTrue();
        }

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, correo);
        using (var cambio = await CambiarAsync(cliente, PasswordInicial, "NuevaContrasena2"))
        {
            cambio.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var despues = await LoginAsync(anonimo, correo, "NuevaContrasena2");

        var final = await despues.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json);
        final!.RequiereCambioPassword.Should().BeFalse(
            "el cambio reinicia la ventana de expiración (RF-003)");
    }
}
