using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// RF-082 por fecha civil para <c>PermisoAcceso</c> PERSONA y aislamiento de alcance (RF-083 (c); F-2;
/// Principio I; research.md §36.2, §36.4).
/// </summary>
/// <remarks>
/// La pertenencia declara los días D1 a D2 (fechas relativas a hoy) y guarda sus límites en UTC. Estos casos
/// son los que distinguen comparar por fecha civil de comparar por instante, y son los que usa la regresión
/// dirigida de T310 (b):
/// <list type="bullet">
/// <item>En Lima (UTC−5), un fin = D2 termina en <c>(D2+1) 04:59:59.999Z</c>, después que la pertenencia.</item>
/// <item>En Tokio (UTC+9), un inicio = D1 empieza en <c>(D1−1) 15:00Z</c>, antes que la pertenencia. En Lima
/// el inicio no los distingue, porque <c>D1 05:00Z</c> no es anterior a <c>D1 00:00Z</c>.</item>
/// </list>
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContencionPermisoFechaCivilTests(SqlServerFixture fixture)
{
    private async Task<(EscenarioPermisos Escenario, AsignacionPersonaCompania Pertenencia, DateOnly D1, DateOnly D2)>
        MontarAsync()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var pertenencia = (await escenario.Us5.PertenenciasEnBaseAsync())[0];

        return (
            escenario,
            pertenencia,
            EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraInicio),
            EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraFin));
    }

    private static Task VerificarAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo) =>
        ContencionPerfilesTests.VerificarProblemaAsync(respuesta, estado, codigo);

    // --- Extremo final: Lima --------------------------------------------------------------------------

    [Fact]
    public async Task En_Lima_un_fin_igual_al_ultimo_dia_de_la_pertenencia_se_acepta_aunque_su_instante_sea_posterior()
    {
        var (escenario, pertenencia, d1, d2) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d1.AddDays(1), fin: d2));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var fila = (await PermisosDelAreaAsync(escenario.Area.Id)).Single();
        fila.FechaHoraFinVigencia.Should().Be(d2.AddDays(1).ToDateTime(new TimeOnly(4, 59, 59, 999), DateTimeKind.Utc));
        fila.FechaHoraFinVigencia.Should().BeAfter(pertenencia.FechaHoraFin, "el fin del permiso en Lima supera el instante de la pertenencia");
    }

    [Fact]
    public async Task En_Lima_un_fin_posterior_al_ultimo_dia_de_la_pertenencia_se_rechaza()
    {
        var (escenario, _, d1, d2) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d1, fin: d2.AddDays(1)));

        await VerificarAsync(respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task En_Lima_un_inicio_igual_al_primer_dia_se_acepta_y_el_dia_anterior_se_rechaza()
    {
        var (escenario, _, d1, d2) = await MontarAsync();

        using (var igual = await escenario.PostPermisoAsync(escenario.Peticion(
                   AlcancePermiso.PERSONA, inicio: d1, fin: d2.AddDays(-1))))
        {
            igual.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var anterior = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d1.AddDays(-1), fin: d2.AddDays(-1)));

        await VerificarAsync(anterior, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().ContainSingle();
    }

    // --- Extremo inicial: zona al este de UTC ------------------------------------------------------------

    [Fact]
    public async Task En_Tokio_un_inicio_igual_al_primer_dia_de_la_pertenencia_se_acepta_aunque_su_instante_sea_anterior()
    {
        var (escenario, pertenencia, d1, d2) = await MontarAsync();

        // Dato de montaje: un área de la Principal B, que pasa a Asia/Tokyo (UTC+9, sin cambio de horario).
        // El alta no exige contexto operativo; basta con que el actor administre el área.
        await CambiarZonaEnBaseAsync(escenario.PrincipalB.Id, "Asia/Tokyo");
        var area = await escenario.CrearAreaAsync(escenario.PrincipalB.Id, "Tokio");

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, areaId: area.Id, inicio: d1, fin: d1.AddDays(10)));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var fila = (await PermisosDelAreaAsync(area.Id)).Single();
        fila.FechaHoraInicioVigencia.Should().Be(d1.AddDays(-1).ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc));
        fila.FechaHoraInicioVigencia.Should().BeBefore(pertenencia.FechaHoraInicio, "el inicio del permiso en Tokio es anterior al instante de la pertenencia");
    }

    // --- Sin pertenencia --------------------------------------------------------------------------------

    [Fact]
    public async Task Una_persona_sin_pertenencia_vigente_se_rechaza_con_400()
    {
        var (escenario, _, _, _) = await MontarAsync();
        var sinPertenencia = await fixture.Api.SembrarPersonaAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, sujetoId: sinPertenencia.Id));

        await VerificarAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.SinPertenenciaVigente);
        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    // --- Aislamiento (Principio I) --------------------------------------------------------------------------

    [Fact]
    public async Task Una_persona_fuera_del_alcance_historico_responde_404_con_cualquier_fecha()
    {
        var (escenario, _, d1, d2) = await MontarAsync();
        using var deLaA = await ClienteDeLaPrincipalAAsync(escenario);

        var ajena = await fixture.Api.SembrarPersonaAsync(companiaId: escenario.PrincipalB.Id);

        // Fechas contenidas y fechas que exceden: la respuesta no puede revelar su pertenencia.
        foreach (var (inicio, fin) in new[] { (d1, d1.AddDays(5)), (d1.AddYears(-1), d2.AddYears(1)) })
        {
            using var respuesta = await deLaA.PostAsJsonAsync(
                new Uri("/api/permisos", UriKind.Relative),
                escenario.Peticion(AlcancePermiso.PERSONA, sujetoId: ajena.Id, inicio: inicio, fin: fin),
                ApiFactory.Json);

            await VerificarAsync(respuesta, HttpStatusCode.NotFound, CodigosError.RecursoNoEncontrado);
        }

        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Un_area_fuera_del_alcance_responde_404_al_crear_y_al_actualizar()
    {
        var (escenario, _, d1, _) = await MontarAsync();
        using var deLaA = await ClienteDeLaPrincipalAAsync(escenario);

        var areaDeB = await escenario.CrearAreaAsync(escenario.PrincipalB.Id, "Ajena");

        using (var alta = await deLaA.PostAsJsonAsync(
                   new Uri("/api/permisos", UriKind.Relative),
                   escenario.Peticion(AlcancePermiso.PERSONA, areaId: areaDeB.Id, inicio: d1, fin: d1.AddDays(5)),
                   ApiFactory.Json))
        {
            await VerificarAsync(alta, HttpStatusCode.NotFound, CodigosError.RecursoNoEncontrado);
        }

        (await PermisosDelAreaAsync(areaDeB.Id)).Should().BeEmpty();

        // Un permiso de esa área, creado por quien sí la administra, tampoco es actualizable desde la A.
        var permiso = await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, areaId: areaDeB.Id, inicio: d1, fin: d1.AddDays(5)));

        using var cambio = await ContencionPermisosPersonaTests.PutAsync(
            deLaA,
            permiso.Id,
            escenario.Peticion(AlcancePermiso.PERSONA, areaId: areaDeB.Id, inicio: d1, fin: d1.AddDays(9)));

        await VerificarAsync(cambio, HttpStatusCode.NotFound, CodigosError.RecursoNoEncontrado);

        var fila = (await PermisosDelAreaAsync(areaDeB.Id)).Single();
        fila.FechaHoraFinVigencia.Should().Be(permiso.FechaHoraFinVigencia);
    }

    // --- Utilidades ---------------------------------------------------------------------------------------

    private async Task<HttpClient> ClienteDeLaPrincipalAAsync(EscenarioPermisos escenario)
    {
        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"vf004.admin-a.{Guid.CreateVersion7():N}@empresa.cl",
            EscenarioUs5.Password,
            alcanceCompanias: [escenario.PrincipalA.Id]);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    private Task CambiarZonaEnBaseAsync(Guid companiaId, string zonaIana) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            var compania = await db.Set<Compania>().SingleAsync(c => c.Id == companiaId);
            compania.ZonaHorariaIana = zonaIana;
            await db.SaveChangesAsync();
        });

    private async Task<List<PermisoAcceso>> PermisosDelAreaAsync(Guid areaId)
    {
        List<PermisoAcceso> resultado = [];

        await fixture.Api.ConDbContextAsync(async db => resultado = await db.Set<PermisoAcceso>()
            .AsNoTracking()
            .Where(p => p.AreaAccesoId == areaId)
            .ToListAsync());

        return resultado;
    }
}
