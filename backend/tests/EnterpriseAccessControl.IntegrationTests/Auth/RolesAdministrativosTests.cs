using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// RBAC, alcance y restricciones de delegación (CS-036, CS-037; RF-074 a RF-077).
/// </summary>
/// <remarks>
/// Cubre el defecto crítico F-01: hasta la Sesión 2026-09-20 <c>UsuarioService</c> no aplicaba
/// ningún control de alcance y cualquier usuario autenticado podía listar y modificar a cualquier
/// otro. Estas pruebas fallan si esa regresión vuelve.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RolesAdministrativosTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private static AsignarRolRequest Peticion(
        RolAdministrativo rol,
        Guid? companiaId,
        DateTime? inicio = null,
        DateTime? fin = null)
    {
        var ahora = DateTime.UtcNow;

        return new AsignarRolRequest(
            rol,
            companiaId,
            inicio ?? ahora.AddDays(-1),
            fin ?? ahora.AddYears(1));
    }

    // --- CS-036: restricciones del COMPANY_ADMINISTRATOR (RF-076) -----------------------------

    [Fact]
    public async Task CS036_no_lista_usuarios_de_otra_compania()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [propia.Id]);

        var deLaAjena = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("ajeno"), Password, alcanceCompanias: [ajena.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        var pagina = await cliente.GetFromJsonAsync<PaginaUsuariosDto>(
            new Uri("/api/usuarios?tamañoPagina=200", UriKind.Relative), ApiFactory.Json);

        var ids = pagina!.Items.Select(u => u.Id).ToList();

        ids.Should().Contain(admin.Id);
        ids.Should().NotContain(deLaAjena.Id, "un usuario de otra compañía no pertenece a su alcance");

        // El total tampoco lo cuenta: revelarlo permitiría deducir cuántos usuarios existen fuera.
        pagina.Total.Should().Be(ids.Count);
    }

    [Fact]
    public async Task CS036_no_modifica_un_usuario_de_otra_compania()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [propia.Id]);

        var deLaAjena = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("ajeno"), Password, alcanceCompanias: [ajena.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/usuarios/{deLaAjena.Id}", UriKind.Relative),
            new ActualizarUsuarioRequest(CorreoUnico("robado"), EstadoUsuario.INACTIVO),
            ApiFactory.Json);

        // 404 y no 403: distinguirlos confirmaría que ese usuario existe (RF-077).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CS036_no_puede_asignarse_a_si_mismo_un_alcance_mayor()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [propia.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var haciaOtraCompania = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{admin.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.COMPANY_ADMINISTRATOR, ajena.Id),
            ApiFactory.Json);

        haciaOtraCompania.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CS036_no_puede_crear_una_asignacion_global()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [propia.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{admin.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.GLOBAL_ADMINISTRATOR, companiaId: null),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CS036_si_puede_crear_pares_de_su_mismo_nivel_en_su_compania()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [propia.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        var ahora = DateTime.UtcNow;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/usuarios", UriKind.Relative),
            new CrearUsuarioRequest(
                CorreoUnico("par"),
                Password,
                RolAdministrativo.COMPANY_ADMINISTRATOR,
                propia.Id,
                ahora.AddDays(-1),
                ahora.AddYears(1)),
            ApiFactory.Json);

        // RF-076: crear pares del mismo nivel en la propia compañía sí está permitido.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CS036_un_administrador_global_si_asigna_cualquier_rol_y_compania()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Cualquiera");

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        var destino = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("destino"), Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.GLOBAL_ADMINISTRATOR, companiaId: null),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- Invariantes de la asignación en la API (RF-074, RF-075) ------------------------------

    [Fact]
    public async Task La_regla_fundamental_se_rechaza_con_400_en_ambas_direcciones()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Regla Fundamental");

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        using var globalConCompania = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{global.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.GLOBAL_ADMINISTRATOR, compania.Id),
            ApiFactory.Json);

        globalConCompania.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var companySinCompania = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{global.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.COMPANY_ADMINISTRATOR, companiaId: null),
            ApiFactory.Json);

        companySinCompania.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_asignacion_solapada_del_mismo_par_usuario_compania_se_rechaza_con_409()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Solapamiento");

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        // SembrarUsuarioAsync ya deja una asignación vigente para (destino, compania).
        var destino = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("destino"), Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.COMPANY_ADMINISTRATOR, compania.Id),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Dos_asignaciones_consecutivas_del_mismo_par_si_se_admiten()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Consecutivas");

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        var destino = await fixture.Api.SembrarUsuarioAsync(CorreoUnico("destino"), Password);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        var inicio = DateTime.UtcNow.AddYears(1);

        using var primera = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative),
            Peticion(RolAdministrativo.COMPANY_ADMINISTRATOR, compania.Id, inicio, inicio.AddYears(1)),
            ApiFactory.Json);

        primera.StatusCode.Should().Be(HttpStatusCode.Created);

        // El fin es exclusivo, así que empezar exactamente donde termina la anterior no solapa.
        using var segunda = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative),
            Peticion(
                RolAdministrativo.COMPANY_ADMINISTRATOR,
                compania.Id,
                inicio.AddYears(1),
                inicio.AddYears(2)),
            ApiFactory.Json);

        segunda.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Finalizar_acorta_la_vigencia_y_conserva_el_registro()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Finalizar");

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        var destino = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("destino"), Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        var roles = await cliente.GetFromJsonAsync<List<AsignacionRolAdministrativoDto>>(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative), ApiFactory.Json);

        var vigente = roles!.Single(r => r.Vigente);

        using var respuesta = await cliente.PostAsync(
            new Uri($"/api/usuarios/{destino.Id}/roles/{vigente.Id}/finalizar", UriKind.Relative),
            content: null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var despues = await cliente.GetFromJsonAsync<List<AsignacionRolAdministrativoDto>>(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative), ApiFactory.Json);

        // La fila sigue existiendo: finalizar acorta la vigencia, nunca borra el histórico.
        despues!.Should().ContainSingle().Which.Vigente.Should().BeFalse();
    }

    // --- Renovación de asignación de rol (RF-075, RF-076, RF-077; cierre de la desviación D-1) ---

    /// <summary>Monta un usuario destino con una asignación vigente y devuelve cliente + ids.</summary>
    private async Task<(HttpClient Cliente, Guid UsuarioId, AsignacionRolAdministrativoDto Vigente)>
        MontarRenovableAsync(Guid? companiaId = null, bool globalDestino = false)
    {
        var compania = companiaId is null
            ? (await fixture.Api.SembrarCompaniaAsync("Renovar")).Id
            : companiaId.Value;

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        var destino = globalDestino
            ? await fixture.Api.SembrarUsuarioAsync(CorreoUnico("destino"), Password, global: true)
            : await fixture.Api.SembrarUsuarioAsync(
                CorreoUnico("destino"), Password, alcanceCompanias: [compania]);

        var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        var roles = await cliente.GetFromJsonAsync<List<AsignacionRolAdministrativoDto>>(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative), ApiFactory.Json);

        return (cliente, destino.Id, roles!.Single(r => r.Vigente));
    }

    private static Uri RutaRenovar(Guid usuarioId, Guid asignacionId) =>
        new($"/api/usuarios/{usuarioId}/roles/{asignacionId}/renovar", UriKind.Relative);

    [Fact]
    public async Task Renovar_extiende_la_vigencia_sin_crear_otra_asignacion()
    {
        var (cliente, usuarioId, vigente) = await MontarRenovableAsync();
        using var _ = cliente;

        var nuevoFin = vigente.FechaHoraFin.AddMonths(6);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaRenovar(usuarioId, vigente.Id),
            new RenovarAsignacionRolRequest(nuevoFin),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var despues = await cliente.GetFromJsonAsync<List<AsignacionRolAdministrativoDto>>(
            new Uri($"/api/usuarios/{usuarioId}/roles", UriKind.Relative), ApiFactory.Json);

        // Es la MISMA asignación: mismo id, rol, compañía e inicio. Solo se movió el techo temporal.
        var renovada = despues!.Should().ContainSingle().Subject;
        renovada.Id.Should().Be(vigente.Id);
        renovada.Rol.Should().Be(vigente.Rol);
        renovada.CompaniaId.Should().Be(vigente.CompaniaId);
        renovada.FechaHoraInicio.Should().Be(vigente.FechaHoraInicio);
        renovada.FechaHoraFin.Should().BeCloseTo(nuevoFin, TimeSpan.FromSeconds(1));
        renovada.Vigente.Should().BeTrue();
    }

    [Fact]
    public async Task Renovar_sin_cuerpo_valido_responde_400()
    {
        var (cliente, usuarioId, vigente) = await MontarRenovableAsync();
        using var _ = cliente;

        using var contenido = new StringContent(
            "{}", System.Text.Encoding.UTF8, "application/json");

        using var respuesta = await cliente.PostAsync(RutaRenovar(usuarioId, vigente.Id), contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Renovar_con_fecha_no_posterior_responde_409_RENOVACION_NO_POSTERIOR()
    {
        var (cliente, usuarioId, vigente) = await MontarRenovableAsync();
        using var _ = cliente;

        // Exactamente la fecha vigente: la regla exige estrictamente posterior.
        using var respuesta = await cliente.PostAsJsonAsync(
            RutaRenovar(usuarioId, vigente.Id),
            new RenovarAsignacionRolRequest(vigente.FechaHoraFin),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await DebeTraerCodigoAsync(respuesta, "RENOVACION_NO_POSTERIOR");
    }

    [Fact]
    public async Task Renovar_una_asignacion_ya_finalizada_responde_409_ASIGNACION_ROL_NO_VIGENTE()
    {
        var (cliente, usuarioId, vigente) = await MontarRenovableAsync();
        using var _ = cliente;

        using var finalizada = await cliente.PostAsync(
            new Uri($"/api/usuarios/{usuarioId}/roles/{vigente.Id}/finalizar", UriKind.Relative),
            content: null);

        finalizada.EnsureSuccessStatusCode();

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaRenovar(usuarioId, vigente.Id),
            new RenovarAsignacionRolRequest(vigente.FechaHoraFin.AddYears(1)),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await DebeTraerCodigoAsync(respuesta, "ASIGNACION_ROL_NO_VIGENTE");
    }

    [Fact]
    public async Task Renovar_hasta_solaparse_con_otra_asignacion_responde_409_SOLAPAMIENTO_VIGENCIA()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Solapar Renovando");

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        var destino = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("destino"), Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        var roles = await cliente.GetFromJsonAsync<List<AsignacionRolAdministrativoDto>>(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative), ApiFactory.Json);

        var vigente = roles!.Single(r => r.Vigente);

        // Una segunda asignación consecutiva para el mismo par (usuario, compañía): válida porque no
        // se solapa. Renovar la primera hasta invadirla sí debe romper el invariante de RF-075.
        using var posterior = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{destino.Id}/roles", UriKind.Relative),
            Peticion(
                RolAdministrativo.COMPANY_ADMINISTRATOR,
                compania.Id,
                vigente.FechaHoraFin.AddDays(1),
                vigente.FechaHoraFin.AddYears(2)),
            ApiFactory.Json);

        posterior.StatusCode.Should().Be(HttpStatusCode.Created);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaRenovar(destino.Id, vigente.Id),
            new RenovarAsignacionRolRequest(vigente.FechaHoraFin.AddMonths(6)),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await DebeTraerCodigoAsync(respuesta, "SOLAPAMIENTO_VIGENCIA");
    }

    [Fact]
    public async Task Renovar_una_asignacion_fuera_de_alcance_responde_404()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [propia.Id]);

        var deLaAjena = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("ajeno"), Password, alcanceCompanias: [ajena.Id]);

        // El id de la asignación ajena se conoce y es válido: aun así no debe ser alcanzable.
        var asignacionAjenaId = Guid.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
            asignacionAjenaId = await db.Set<AsignacionRolAdministrativo>()
                .Where(a => a.UsuarioId == deLaAjena.Id)
                .Select(a => a.Id)
                .SingleAsync());

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaRenovar(deLaAjena.Id, asignacionAjenaId),
            new RenovarAsignacionRolRequest(DateTime.UtcNow.AddYears(2)),
            ApiFactory.Json);

        // 404 y no 403: distinguirlos confirmaría que ese usuario existe (RF-077).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Renovar_una_asignacion_global_desde_company_admin_responde_403()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Company Admin");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, alcanceCompanias: [compania.Id]);

        // El destino está dentro de su alcance (comparte compañía), pero además tiene una asignación
        // GLOBAL que un COMPANY_ADMINISTRATOR no podría crear — y por tanto tampoco extender (RF-076).
        var destino = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("destino"), Password, alcanceCompanias: [compania.Id], global: true);

        var asignacionGlobalId = Guid.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
            asignacionGlobalId = await db.Set<AsignacionRolAdministrativo>()
                .Where(a => a.UsuarioId == destino.Id
                            && a.Rol == RolAdministrativo.GLOBAL_ADMINISTRATOR)
                .Select(a => a.Id)
                .SingleAsync());

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaRenovar(destino.Id, asignacionGlobalId),
            new RenovarAsignacionRolRequest(DateTime.UtcNow.AddYears(3)),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await DebeTraerCodigoAsync(respuesta, "ROL_NO_AUTORIZADO");
    }

    /// <summary>El código de negocio viaja en `ProblemDetails.codigo` (research.md §21).</summary>
    private static async Task DebeTraerCodigoAsync(HttpResponseMessage respuesta, string codigo)
    {
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);
        problema!.Codigo.Should().Be(codigo);
    }

    private sealed record ProblemaDto(string? Codigo);

    // --- CS-038: bootstrap idempotente (RF-078) -----------------------------------------------

    [Fact]
    public async Task CS038_el_arranque_deja_exactamente_un_administrador_global_con_cambio_pendiente()
    {
        // La API del fixture ya arrancó al menos una vez, así que la siembra ya ocurrió.
        using var cliente = fixture.Api.CrearCliente();
        cliente.Dispose();

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var correo = Usuario.NormalizarCorreo(ApiFactory.CorreoBootstrapPruebas);

            var sembrados = await db.Set<Usuario>()
                .Where(u => u.CorreoNormalizado == correo)
                .ToListAsync();

            // Reiniciar la aplicación no crea un segundo: la condición es la ausencia de cualquier
            // GLOBAL_ADMINISTRATOR, no la del usuario del arranque.
            sembrados.Should().ContainSingle();
            sembrados[0].RequiereCambioPassword.Should().BeTrue();

            var asignaciones = await db.Set<AsignacionRolAdministrativo>()
                .Where(a => a.UsuarioId == sembrados[0].Id)
                .ToListAsync();

            asignaciones.Should().ContainSingle();
            asignaciones[0].Rol.Should().Be(RolAdministrativo.GLOBAL_ADMINISTRATOR);
            asignaciones[0].CompaniaId.Should().BeNull();
        });
    }

    [Fact]
    public async Task CS038_la_fecha_centinela_no_aparece_en_ninguna_otra_asignacion()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Sin Centinela");

        await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("normal"), Password, alcanceCompanias: [compania.Id]);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var correo = Usuario.NormalizarCorreo(ApiFactory.CorreoBootstrapPruebas);

            var bootstrapId = await db.Set<Usuario>()
                .Where(u => u.CorreoNormalizado == correo)
                .Select(u => u.Id)
                .SingleAsync();

            var conCentinela = await db.Set<AsignacionRolAdministrativo>()
                .Where(a => a.FechaHoraFin == Domain.Common.VigenciaBootstrap.MaxValidityDate)
                .ToListAsync();

            // La excepción a RF-071/RF-075 es exclusiva de la fila sembrada por el arranque.
            conCentinela.Should().OnlyContain(a => a.UsuarioId == bootstrapId);
        });
    }

    /// <summary>Proyección mínima de la página de usuarios, para no depender del DTO completo.</summary>
    private sealed record PaginaUsuariosDto(List<UsuarioResumen> Items, int Total);

    private sealed record UsuarioResumen(Guid Id, string Correo);
}
