using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// CS-042 sobre <c>PermisoAcceso</c>: contención temporal de RF-082 solo para el alcance PERSONA (D2),
/// en las operaciones que D4 somete a ella, y aislamiento de alcance antes de consultar la pertenencia.
/// </summary>
/// <remarks>
/// Cambio de requisito post-Baseline VF-007. Todo pasa por HTTP con un actor autenticado: lo que se
/// verifica es la regla del servidor, no la de la interfaz (Principio I).
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContencionPermisosPersonaTests(SqlServerFixture fixture)
{
    /// <remarks>
    /// Desde VF-004 (RF-083) la vigencia del permiso son fechas civiles y la contención se compara con las
    /// fechas que declara la pertenencia, nunca con su instante convertido a Lima.
    /// </remarks>
    private async Task<(EscenarioPermisos Escenario, DateOnly Desde, DateOnly Hasta)> MontarAsync()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var pertenencia = (await escenario.Us5.PertenenciasEnBaseAsync())[0];

        return (
            escenario,
            EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraInicio),
            EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraFin));
    }

    private static Task VerificarAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo) =>
        ContencionPerfilesTests.VerificarProblemaAsync(respuesta, estado, codigo);

    // --- Alta con alcance PERSONA (CS-042) ----------------------------------------------------------

    [Fact]
    public async Task CS042_a_un_permiso_persona_que_termina_antes_que_la_pertenencia_se_acepta()
    {
        var (escenario, desde, hasta) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: desde, fin: hasta.AddDays(-30)));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CS042_b_un_permiso_persona_con_las_mismas_fechas_que_la_pertenencia_se_acepta()
    {
        var (escenario, desde, hasta) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: desde, fin: hasta));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CS042_c_un_permiso_persona_que_termina_despues_que_la_pertenencia_se_rechaza()
    {
        var (escenario, desde, hasta) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: desde, fin: hasta.AddDays(1)));

        await VerificarAsync(respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task CS042_d_un_permiso_persona_que_empieza_antes_que_la_pertenencia_se_rechaza()
    {
        var (escenario, desde, hasta) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: desde.AddDays(-1), fin: hasta));

        await VerificarAsync(respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task CS042_e_un_permiso_persona_para_quien_no_tiene_pertenencia_se_rechaza()
    {
        var (escenario, _, _) = await MontarAsync();

        // Sin ningún histórico, la persona es administrable por cualquiera (como en el alta de perfil),
        // así que la operación llega a consultar la pertenencia y no la encuentra.
        var sinPertenencia = await fixture.Api.SembrarPersonaAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, sujetoId: sinPertenencia.Id));

        await VerificarAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.SinPertenenciaVigente);
    }

    [Fact]
    public async Task Un_alta_persona_inactiva_no_se_contiene()
    {
        var (escenario, desde, hasta) = await MontarAsync();

        // D4: un permiso que nace INACTIVO no concede acceso, así que no consulta la pertenencia.
        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: desde.AddYears(-1),
            fin: hasta.AddYears(1),
            estado: Estado.INACTIVO));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- Alcances fuera de RF-082 (D2) ----------------------------------------------------------------

    [Theory]
    [InlineData(AlcancePermiso.UNIDAD_ORGANIZATIVA)]
    [InlineData(AlcancePermiso.COMPANIA)]
    public async Task Los_permisos_no_persona_no_se_contienen_al_crear_ni_al_actualizar(AlcancePermiso alcance)
    {
        var (escenario, desde, hasta) = await MontarAsync();

        using (var alta = await escenario.PostPermisoAsync(escenario.Peticion(
            alcance, inicio: desde.AddDays(-1), fin: hasta.AddDays(1))))
        {
            alta.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(alcance));

        using var cambio = await PutAsync(
            escenario.Cliente,
            creado.Id,
            escenario.Peticion(alcance, inicio: desde.AddYears(-1), fin: hasta.AddYears(1)));

        cambio.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- Cambio de fechas de un permiso PERSONA (D4) ---------------------------------------------------

    [Fact]
    public async Task Cambiar_las_fechas_a_un_rango_contenido_se_acepta()
    {
        var (escenario, desde, hasta) = await MontarAsync();
        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(AlcancePermiso.PERSONA));

        using var respuesta = await PutAsync(
            escenario.Cliente,
            creado.Id,
            escenario.Peticion(AlcancePermiso.PERSONA, inicio: desde, fin: hasta.AddDays(-10)));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cambiar_las_fechas_a_un_rango_que_excede_la_pertenencia_se_rechaza()
    {
        var (escenario, desde, hasta) = await MontarAsync();
        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(AlcancePermiso.PERSONA));

        using var respuesta = await PutAsync(
            escenario.Cliente,
            creado.Id,
            escenario.Peticion(AlcancePermiso.PERSONA, inicio: desde, fin: hasta.AddMonths(1)));

        await VerificarAsync(respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task Cambiar_las_fechas_cuando_la_persona_ya_no_tiene_pertenencia_se_rechaza()
    {
        var (escenario, _, _) = await MontarAsync();
        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(AlcancePermiso.PERSONA));

        await VencerPertenenciaAsync(escenario);

        using var respuesta = await PutAsync(
            escenario.Cliente,
            creado.Id,
            escenario.Peticion(
                AlcancePermiso.PERSONA,
                inicio: escenario.FechaCivil(EscenarioPermisos.Instante.AddMonths(-1)),
                fin: escenario.FechaCivil(EscenarioPermisos.Instante.AddMonths(2))));

        await VerificarAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.SinPertenenciaVigente);
    }

    // --- Aislamiento entre compañías (hallazgo C1, research.md §35.6) ---------------------------------

    [Fact]
    public async Task Crear_un_permiso_persona_para_alguien_fuera_de_alcance_responde_404_sin_revelar_su_pertenencia()
    {
        var (escenario, _, _) = await MontarAsync();
        using var deLaA = await ClienteDeLaPrincipalAAsync(escenario);

        // Persona cuyo único histórico es con la Principal B: fuera del alcance de un admin de la A.
        var ajena = await fixture.Api.SembrarPersonaAsync(companiaId: escenario.PrincipalB.Id);

        var ahora = DateOnly.FromDateTime(DateTime.UtcNow);

        // Con fechas contenidas en su pertenencia y con fechas que la exceden: si la contención se
        // evaluara antes que el alcance, la diferencia de respuesta revelaría esa pertenencia.
        foreach (var (inicio, fin) in new[] { (ahora, ahora.AddMonths(1)), (ahora.AddYears(-1), ahora.AddYears(2)) })
        {
            using var respuesta = await deLaA.PostAsJsonAsync(
                new Uri("/api/permisos", UriKind.Relative),
                escenario.Peticion(AlcancePermiso.PERSONA, sujetoId: ajena.Id, inicio: inicio, fin: fin),
                ApiFactory.Json);

            await VerificarAsync(respuesta, HttpStatusCode.NotFound, CodigosError.RecursoNoEncontrado);
        }

        (await PermisosDeAsync(ajena.Id)).Should().BeEmpty("un rechazo por alcance no persiste nada");
    }

    [Fact]
    public async Task Actualizar_las_fechas_de_un_permiso_de_alguien_fuera_de_alcance_responde_404_sin_revelar_su_pertenencia()
    {
        var (escenario, _, _) = await MontarAsync();
        using var deLaA = await ClienteDeLaPrincipalAAsync(escenario);

        var ajena = await fixture.Api.SembrarPersonaAsync(companiaId: escenario.PrincipalB.Id);
        var ahora = DateOnly.FromDateTime(DateTime.UtcNow);

        // Lo crea quien sí administra a la persona (el admin del escenario alcanza también la B).
        var permiso = await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, sujetoId: ajena.Id, inicio: ahora, fin: ahora.AddMonths(1)));

        // El área es de la A, así que el admin de la A sí ve el permiso; lo que no alcanza es la persona.
        foreach (var fin in new[] { ahora.AddMonths(2), ahora.AddYears(3) })
        {
            using var respuesta = await PutAsync(
                deLaA,
                permiso.Id,
                escenario.Peticion(AlcancePermiso.PERSONA, sujetoId: ajena.Id, inicio: ahora, fin: fin));

            await VerificarAsync(respuesta, HttpStatusCode.NotFound, CodigosError.RecursoNoEncontrado);
        }

        var almacenado = (await PermisosDeAsync(ajena.Id)).Single();
        almacenado.FechaHoraFinVigencia.Should().BeCloseTo(permiso.FechaHoraFinVigencia, TimeSpan.FromMilliseconds(1));
    }

    // --- Utilidades ------------------------------------------------------------------------------------

    internal static Task<HttpResponseMessage> PutAsync(
        HttpClient cliente,
        Guid permisoId,
        PermisoAccesoRequest peticion) =>
        cliente.PutAsJsonAsync(new Uri($"/api/permisos/{permisoId}", UriKind.Relative), peticion, ApiFactory.Json);

    private async Task<HttpClient> ClienteDeLaPrincipalAAsync(EscenarioPermisos escenario)
    {
        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"vf007.admin-a.{Guid.CreateVersion7():N}@empresa.cl",
            EscenarioUs5.Password,
            alcanceCompanias: [escenario.PrincipalA.Id]);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    /// <summary>Deja la pertenencia vigente de la persona terminada en el pasado, sin la cascada.</summary>
    internal static Task VencerPertenenciaAsync(EscenarioPermisos escenario) =>
        escenario.Us5.ConDatosAsync(async db =>
        {
            var pertenencia = await db.Set<AsignacionPersonaCompania>()
                .FirstAsync(a => a.PersonaId == escenario.Persona.Id);

            pertenencia.FechaHoraFin = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

    private async Task<List<PermisoAcceso>> PermisosDeAsync(Guid personaId)
    {
        List<PermisoAcceso> resultado = [];

        await fixture.Api.ConDbContextAsync(async db => resultado = await db.Set<PermisoAcceso>()
            .AsNoTracking()
            .Where(p => p.PersonaId == personaId)
            .ToListAsync());

        return resultado;
    }
}
