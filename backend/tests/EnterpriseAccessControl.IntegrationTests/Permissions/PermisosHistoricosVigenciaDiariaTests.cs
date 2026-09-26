using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Permisos anteriores a VF-004, edición por extremo y cambio de zona (CS-046, CS-047; RF-083 (d), (g), (h);
/// F-4, F-6, F-7).
/// </summary>
/// <remarks>
/// Las fechas son relativas a <see cref="EscenarioPermisos.Instante"/>: D es su fecha civil en Lima, Da = D − 5,
/// Db = Da + 5 y Dx = Da + 2. El escenario garantiza que Da − 1, Da, Dx, Db y Db + 5 caen dentro de la
/// pertenencia (de hace un mes a dentro de un año), así que los <c>PUT</c> que cambian una fecha de un permiso
/// PERSONA <c>ACTIVO</c> se aceptan o rechazan por la regla probada y no por salirse de la pertenencia. El ejemplo
/// <c>2026-09-25T08:00Z – 2026-09-30T17:00Z</c> de spec.md CS-046 queda solo como referencia. Se mantienen sus
/// horas: 08:00Z y 17:00Z (03:00 y 12:00 en Lima). Los permisos anteriores se siembran directamente en base
/// de datos, como en <c>RegistrosAnterioresRf082Tests</c>.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PermisosHistoricosVigenciaDiariaTests(SqlServerFixture fixture)
{
    private sealed record Historico(EscenarioPermisos Escenario, Guid PermisoId, DateOnly Da, DateOnly Db);

    private static DateTime InicioLima(DateOnly fecha) => fecha.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc);

    private static DateTime FinLima(DateOnly fecha) =>
        fecha.AddDays(1).ToDateTime(new TimeOnly(4, 59, 59, 999), DateTimeKind.Utc);

    private async Task<Historico> SembrarAsync()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var d = escenario.FechaCivil(EscenarioPermisos.Instante);
        var da = d.AddDays(-5);
        var db = da.AddDays(5);

        var id = await SembrarPermisoAsync(
            escenario,
            da.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc),
            db.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc));

        return new Historico(escenario, id, da, db);
    }

    // --- CS-046 (a), (b): consulta ------------------------------------------------------------------------

    [Fact]
    public async Task CS046_un_permiso_con_hora_se_lee_con_sus_instantes_y_no_como_dias_completos()
    {
        var h = await SembrarAsync();

        var leido = await LeerAsync(h.Escenario, h.PermisoId);

        leido.FechaHoraInicioVigencia.Should().Be(h.Da.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc));
        leido.FechaHoraFinVigencia.Should().Be(h.Db.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc));
        leido.FechaInicioVigencia.Should().Be(h.Da);
        leido.FechaFinVigencia.Should().Be(h.Db);
        leido.VigenciaEnDiasCompletos.Should().BeFalse();
        leido.ZonaHorariaIana.Should().Be("America/Lima");
    }

    // --- CS-046 (c): solo bloques o estado ------------------------------------------------------------------

    [Fact]
    public async Task CS046_reenviar_las_mismas_fechas_cambiando_solo_los_bloques_conserva_ambos_instantes()
    {
        var h = await SembrarAsync();
        var antes = await FilaAsync(h.PermisoId);

        using var respuesta = await PutAsync(h, h.Da, h.Db, Estado.ACTIVO,
            [new BloqueHorarioRequest(h.Escenario.DiaEvaluado, "06:00", "20:00")]);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarInstantesAsync(h.PermisoId, antes.FechaHoraInicioVigencia, antes.FechaHoraFinVigencia);
    }

    [Fact]
    public async Task CS046_cambiar_solo_el_estado_conserva_ambos_instantes_y_desactivar_no_consulta_la_pertenencia()
    {
        var h = await SembrarAsync();
        var antes = await FilaAsync(h.PermisoId);

        // Sin pertenencia vigente: si desactivar la consultara, respondería 400 SIN_PERTENENCIA_VIGENTE (D4).
        await ContencionPermisosPersonaTests.VencerPertenenciaAsync(h.Escenario);

        using var respuesta = await PutAsync(h, h.Da, h.Db, Estado.INACTIVO);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await FilaAsync(h.PermisoId)).Estado.Should().Be(Estado.INACTIVO);
        await VerificarInstantesAsync(h.PermisoId, antes.FechaHoraInicioVigencia, antes.FechaHoraFinVigencia);
    }

    // --- CS-046 (d): edición por extremo (F-6) ----------------------------------------------------------------

    [Fact]
    public async Task CS046_cambiar_solo_la_fecha_de_fin_normaliza_el_fin_y_conserva_el_inicio()
    {
        var h = await SembrarAsync();
        var antes = await FilaAsync(h.PermisoId);

        using var respuesta = await PutAsync(h, h.Da, h.Db.AddDays(5), Estado.ACTIVO);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarInstantesAsync(h.PermisoId, antes.FechaHoraInicioVigencia, FinLima(h.Db.AddDays(5)));
    }

    [Fact]
    public async Task CS046_cambiar_solo_la_fecha_de_inicio_normaliza_el_inicio_y_conserva_el_fin()
    {
        var h = await SembrarAsync();
        var antes = await FilaAsync(h.PermisoId);

        using var respuesta = await PutAsync(h, h.Da.AddDays(-1), h.Db, Estado.ACTIVO);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarInstantesAsync(h.PermisoId, InicioLima(h.Da.AddDays(-1)), antes.FechaHoraFinVigencia);
    }

    [Fact]
    public async Task Una_edicion_por_extremo_que_deja_los_instantes_invertidos_se_rechaza_sin_modificar_el_permiso()
    {
        var h = await SembrarAsync();
        var dx = h.Da.AddDays(2);

        // Permiso anterior cuyo fin es exactamente las 00:00 de Lima del día Dx: su fecha civil de fin es Dx.
        var id = await SembrarPermisoAsync(
            h.Escenario,
            h.Da.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc),
            InicioLima(dx));
        var antes = await FilaAsync(id);

        // Sin pertenencia: si la contención se consultara antes que el periodo, el código sería otro.
        await ContencionPermisosPersonaTests.VencerPertenenciaAsync(h.Escenario);

        // Inicio = fin = Dx pasa la validación de fechas, pero conserva el fin (Dx 05:00Z) y normaliza el inicio
        // a Dx 05:00Z: FinUtc <= InicioUtc (U1).
        using var respuesta = await ContencionPermisosPersonaTests.PutAsync(
            h.Escenario.Cliente,
            id,
            h.Escenario.Peticion(AlcancePermiso.PERSONA, inicio: dx, fin: dx));

        await ContencionPerfilesTests.VerificarProblemaAsync(
            respuesta, HttpStatusCode.BadRequest, CodigosError.PeriodoInvalido);

        var despues = await FilaAsync(id);
        despues.FechaHoraInicioVigencia.Should().Be(antes.FechaHoraInicioVigencia);
        despues.FechaHoraFinVigencia.Should().Be(antes.FechaHoraFinVigencia);
        despues.Estado.Should().Be(antes.Estado);
        despues.UpdatedAt.Should().Be(antes.UpdatedAt);
        (await BloquesAsync(id)).Should().ContainSingle();
    }

    // --- CS-047: cambio de zona (F-7) ---------------------------------------------------------------------

    [Fact]
    public async Task CS047_cambiar_la_zona_de_la_Principal_no_modifica_instantes_y_recalcula_la_lectura()
    {
        var h = await SembrarAsync();
        var d = h.Db;

        // (1) Un permiso nuevo, normalizado en Lima.
        var nuevo = await h.Escenario.CrearPermisoAsync(h.Escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d, fin: d.AddDays(3)));

        nuevo.VigenciaEnDiasCompletos.Should().BeTrue();
        nuevo.FechaHoraInicioVigencia.Should().Be(InicioLima(d));
        nuevo.FechaHoraFinVigencia.Should().Be(FinLima(d.AddDays(3)));

        // (2) Instantes de todas las filas del área: el permiso nuevo y el anterior sembrado.
        var antes = await InstantesDelAreaAsync(h.Escenario.Area.Id);
        antes.Should().HaveCount(2);

        // (3) Cambio de zona por la API de compañías.
        await CambiarZonaPorApiAsync(h.Escenario, h.Escenario.PrincipalA.Id, "America/Santiago");

        // (4) Ninguna fila cambia; la lectura se recalcula con la zona nueva.
        (await InstantesDelAreaAsync(h.Escenario.Area.Id)).Should().Equal(antes);

        var santiago = DateTimeZoneProviders.Tzdb["America/Santiago"];
        var leido = await LeerAsync(h.Escenario, nuevo.Id);

        leido.ZonaHorariaIana.Should().Be("America/Santiago");
        leido.VigenciaEnDiasCompletos.Should().BeFalse("05:00Z no es el inicio de un día en Santiago");
        leido.FechaInicioVigencia.Should().Be(FechaEn(santiago, nuevo.FechaHoraInicioVigencia));
        leido.FechaFinVigencia.Should().Be(FechaEn(santiago, nuevo.FechaHoraFinVigencia));
        leido.FechaHoraInicioVigencia.Should().Be(nuevo.FechaHoraInicioVigencia);
        leido.FechaHoraFinVigencia.Should().Be(nuevo.FechaHoraFinVigencia);
    }

    // --- Utilidades ---------------------------------------------------------------------------------------

    private static DateOnly FechaEn(DateTimeZone zona, DateTime instanteUtc) =>
        Instant.FromDateTimeUtc(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc)).InZone(zona).Date.ToDateOnly();

    private static Task<HttpResponseMessage> PutAsync(
        Historico h,
        DateOnly inicio,
        DateOnly fin,
        Estado estado,
        IReadOnlyList<BloqueHorarioRequest>? bloques = null) =>
        ContencionPermisosPersonaTests.PutAsync(
            h.Escenario.Cliente,
            h.PermisoId,
            h.Escenario.Peticion(AlcancePermiso.PERSONA, inicio: inicio, fin: fin, estado: estado, bloques: bloques));

    private static async Task<Guid> SembrarPermisoAsync(EscenarioPermisos escenario, DateTime inicio, DateTime fin)
    {
        var permiso = new PermisoAcceso
        {
            AreaAccesoId = escenario.Area.Id,
            Alcance = AlcancePermiso.PERSONA,
            PersonaId = escenario.Persona.Id,
            FechaHoraInicioVigencia = inicio,
            FechaHoraFinVigencia = fin,
            Estado = Estado.ACTIVO,
        };

        await escenario.Us5.ConDatosAsync(async db =>
        {
            db.Set<PermisoAcceso>().Add(permiso);
            db.Set<BloqueHorarioPermiso>().Add(new BloqueHorarioPermiso
            {
                PermisoAccesoId = permiso.Id,
                DiaSemana = escenario.DiaEvaluado,
                HoraInicio = new TimeOnly(8, 0),
                HoraFin = new TimeOnly(17, 0),
            });

            await db.SaveChangesAsync();
        });

        return permiso.Id;
    }

    private static async Task<PermisoAccesoDto> LeerAsync(EscenarioPermisos escenario, Guid id) =>
        (await escenario.Cliente.GetFromJsonAsync<PermisoAccesoDto>(
            new Uri($"/api/permisos/{id}", UriKind.Relative), ApiFactory.Json))!;

    private static async Task CambiarZonaPorApiAsync(EscenarioPermisos escenario, Guid companiaId, string zonaIana)
    {
        var compania = (await escenario.Cliente.GetFromJsonAsync<CompaniaDto>(
            new Uri($"/api/companias/{companiaId}", UriKind.Relative), ApiFactory.Json))!;

        using var respuesta = await escenario.Cliente.PutAsJsonAsync(
            new Uri($"/api/companias/{companiaId}", UriKind.Relative),
            new CompaniaRequest(
                compania.Nombre,
                compania.TipoDocumentoId,
                compania.NumeroDocumento,
                compania.TipoCompania,
                compania.Estado,
                zonaIana),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task VerificarInstantesAsync(Guid id, DateTime inicio, DateTime fin)
    {
        var fila = await FilaAsync(id);

        fila.FechaHoraInicioVigencia.Should().Be(inicio);
        fila.FechaHoraFinVigencia.Should().Be(fin);
    }

    private async Task<PermisoAcceso> FilaAsync(Guid id)
    {
        PermisoAcceso fila = null!;

        await fixture.Api.ConDbContextAsync(async db =>
            fila = await db.Set<PermisoAcceso>().AsNoTracking().SingleAsync(p => p.Id == id));

        return fila;
    }

    private async Task<List<BloqueHorarioPermiso>> BloquesAsync(Guid permisoId)
    {
        List<BloqueHorarioPermiso> bloques = [];

        await fixture.Api.ConDbContextAsync(async db => bloques = await db.Set<BloqueHorarioPermiso>()
            .AsNoTracking()
            .Where(b => b.PermisoAccesoId == permisoId)
            .ToListAsync());

        return bloques;
    }

    private async Task<List<(Guid Id, DateTime Inicio, DateTime Fin)>> InstantesDelAreaAsync(Guid areaId)
    {
        List<(Guid, DateTime, DateTime)> instantes = [];

        await fixture.Api.ConDbContextAsync(async db => instantes = (await db.Set<PermisoAcceso>()
                .AsNoTracking()
                .Where(p => p.AreaAccesoId == areaId)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.FechaHoraInicioVigencia, p.FechaHoraFinVigencia })
                .ToListAsync())
            .Select(p => (p.Id, p.FechaHoraInicioVigencia, p.FechaHoraFinVigencia))
            .ToList());

        return instantes;
    }
}
