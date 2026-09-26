using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// CS-044 y CS-045: vigencia diaria del permiso de acceso por HTTP contra SQL Server real (RF-083; VF-004).
/// </summary>
/// <remarks>
/// Todas las fechas se derivan de <see cref="EscenarioPermisos.Instante"/>: D es su fecha civil en Lima.
/// Las fechas de spec.md CS-044 (25/09–30/09/2026) son solo un ejemplo documental. En <c>America/Lima</c>
/// (UTC−5 fijo, sin cambio de horario), la fecha de inicio D es <c>D 05:00:00.000Z</c> y la fecha de fin D es
/// <c>(D+1) 04:59:59.999Z</c>. Los esperados se calculan con esa regla y no con el código bajo prueba.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class VigenciaDiariaPermisosTests(SqlServerFixture fixture)
{
    private static DateTime InicioLima(DateOnly fecha) => fecha.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc);

    private static DateTime FinLima(DateOnly fecha) =>
        fecha.AddDays(1).ToDateTime(new TimeOnly(4, 59, 59, 999), DateTimeKind.Utc);

    /// <summary>Bloques de 00:00 a 23:59 todos los días: el horario nunca decide en estos casos.</summary>
    private static IReadOnlyList<BloqueHorarioRequest> TodoElDia() =>
        [.. Enum.GetValues<DiaSemana>().Select(d => new BloqueHorarioRequest(d, "00:00", "23:59"))];

    private async Task<(EscenarioPermisos Escenario, DateOnly D)> MontarAsync()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        return (escenario, escenario.FechaCivil(EscenarioPermisos.Instante));
    }

    // --- CS-044 ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CS044_un_alta_diaria_persiste_los_limites_del_dia_en_Lima_y_se_lee_como_fechas()
    {
        var (escenario, d) = await MontarAsync();

        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d, fin: d.AddDays(5), bloques: TodoElDia()));

        var fila = await FilaAsync(creado.Id);
        fila.FechaHoraInicioVigencia.Should().Be(InicioLima(d));
        fila.FechaHoraFinVigencia.Should().Be(FinLima(d.AddDays(5)));

        creado.FechaInicioVigencia.Should().Be(d);
        creado.FechaFinVigencia.Should().Be(d.AddDays(5));
        creado.VigenciaEnDiasCompletos.Should().BeTrue();
        creado.ZonaHorariaIana.Should().Be("America/Lima");
        creado.FechaHoraInicioVigencia.Should().Be(InicioLima(d));
        creado.FechaHoraFinVigencia.Should().Be(FinLima(d.AddDays(5)));
    }

    [Fact]
    public async Task CS044_el_ultimo_dia_concede_hasta_su_final_y_el_siguiente_queda_fuera_de_vigencia()
    {
        var (escenario, d) = await MontarAsync();

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d, fin: d.AddDays(5), bloques: TodoElDia()));

        // D+5 a las 23:00 de Lima = (D+6) 04:00Z: dentro del último día.
        var ultimoDia = await escenario.EvaluarAsync(fechaHora: d.AddDays(6).ToDateTime(new TimeOnly(4, 0), DateTimeKind.Utc));
        ultimoDia.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        // D+6 a las 00:30 de Lima = (D+6) 05:30Z: el día siguiente ya no está en la vigencia.
        var diaSiguiente = await escenario.EvaluarAsync(fechaHora: d.AddDays(6).ToDateTime(new TimeOnly(5, 30), DateTimeKind.Utc));
        diaSiguiente.MotivoDenegacion.Should().Be(MotivoDenegacion.PERMISO_FUERA_DE_VIGENCIA);
    }

    [Fact]
    public async Task CS044_dentro_de_la_vigencia_una_hora_sin_bloque_se_deniega_por_horario()
    {
        var (escenario, d) = await MontarAsync();

        // La restricción dentro del día es de los bloques (RF-022): 08:00–17:00 todos los días.
        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d, fin: d.AddDays(5), bloques: EscenarioPermisos.BloquesTodaLaSemana()));

        // D+2 a las 20:00 de Lima = (D+3) 01:00Z.
        var resultado = await escenario.EvaluarAsync(fechaHora: d.AddDays(3).ToDateTime(new TimeOnly(1, 0), DateTimeKind.Utc));

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO);
    }

    // --- CS-045 ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CS045_un_permiso_de_un_solo_dia_cubre_ese_dia_completo()
    {
        var (escenario, d) = await MontarAsync();

        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d, fin: d));

        var fila = await FilaAsync(creado.Id);
        fila.FechaHoraInicioVigencia.Should().Be(InicioLima(d));
        fila.FechaHoraFinVigencia.Should().Be(FinLima(d));
    }

    [Fact]
    public async Task CS045_una_fecha_de_fin_anterior_al_inicio_se_rechaza_sin_escribir()
    {
        var (escenario, d) = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, inicio: d, fin: d.AddDays(-1)));

        await ContencionPerfilesTests.VerificarProblemaAsync(
            respuesta, HttpStatusCode.BadRequest, CodigosError.PeriodoInvalido);
        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task CS045_un_cuerpo_del_contrato_anterior_se_rechaza_nombrando_los_campos_nuevos()
    {
        var (escenario, _) = await MontarAsync();

        // Un cliente de v1.x: vigencia como date-time. La API no interpreta ese instante (F-3, v2.0.0).
        var cuerpo = CuerpoBase(escenario);
        cuerpo["fechaHoraInicioVigencia"] = EscenarioPermisos.Instante.AddMonths(-1);
        cuerpo["fechaHoraFinVigencia"] = EscenarioPermisos.Instante.AddMonths(1);

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative), cuerpo, ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(ApiFactory.Json);
        problema!.Errors.Keys.Should().Contain(k => string.Equals(k, "fechaInicioVigencia", StringComparison.OrdinalIgnoreCase));
        problema.Errors.Keys.Should().Contain(k => string.Equals(k, "fechaFinVigencia", StringComparison.OrdinalIgnoreCase));

        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-09-25T00:00:00Z")]
    [InlineData("25/09/2026")]
    public async Task CS045_una_fecha_con_hora_o_mal_formada_se_rechaza(string fecha)
    {
        var (escenario, d) = await MontarAsync();

        var cuerpo = CuerpoBase(escenario);
        cuerpo["fechaInicioVigencia"] = fecha;
        cuerpo["fechaFinVigencia"] = d.AddDays(5).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative), cuerpo, ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    // --- Cambio de horario de extremo a extremo ------------------------------------------------------------

    [Fact]
    public async Task En_Santiago_un_alta_que_empieza_el_dia_sin_00_00_persiste_su_primer_instante_valido()
    {
        var (escenario, _) = await MontarAsync();
        var santiago = DateTimeZoneProviders.Tzdb["America/Santiago"];

        // Dato de montaje: la Principal B pasa a Santiago (el usuario del escenario la administra).
        await CambiarZonaEnBaseAsync(escenario.PrincipalB.Id, "America/Santiago");
        var area = await escenario.CrearAreaAsync(escenario.PrincipalB.Id, "Santiago");

        var dia = ProximoDiaSinMedianoche(santiago, EscenarioPermisos.Instante);
        santiago.AtStartOfDay(dia).TimeOfDay.Should().NotBe(LocalTime.Midnight, "premisa: ese día no tiene 00:00");

        // Alcance COMPANIA: no depende de la pertenencia de ninguna persona.
        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.COMPANIA,
            sujetoId: escenario.PrincipalB.Id,
            areaId: area.Id,
            inicio: dia.ToDateOnly(),
            fin: dia.ToDateOnly()));

        var inicioLocal = Instant.FromDateTimeUtc(DateTime.SpecifyKind((await FilaAsync(creado.Id)).FechaHoraInicioVigencia, DateTimeKind.Utc))
            .InZone(santiago);

        inicioLocal.Date.Should().Be(dia);
        inicioLocal.TimeOfDay.Should().Be(new LocalTime(1, 0));
        creado.ZonaHorariaIana.Should().Be("America/Santiago");
        creado.VigenciaEnDiasCompletos.Should().BeTrue();
    }

    // --- Alcances sin contención ---------------------------------------------------------------------------

    [Theory]
    [InlineData(AlcancePermiso.UNIDAD_ORGANIZATIVA)]
    [InlineData(AlcancePermiso.COMPANIA)]
    public async Task Los_alcances_unidad_y_compania_se_crean_fuera_de_la_pertenencia(AlcancePermiso alcance)
    {
        var (escenario, _) = await MontarAsync();
        var pertenencia = (await escenario.Us5.PertenenciasEnBaseAsync())[0];

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            alcance,
            inicio: EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraInicio).AddDays(-1),
            fin: EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraFin).AddDays(1)));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- Utilidades ---------------------------------------------------------------------------------------

    private static Dictionary<string, object?> CuerpoBase(EscenarioPermisos escenario) => new()
    {
        ["areaAccesoId"] = escenario.Area.Id,
        ["alcance"] = "PERSONA",
        ["personaId"] = escenario.Persona.Id,
        ["estado"] = "ACTIVO",
        ["bloquesHorarios"] = new[] { new { diaSemana = "LUNES", horaInicio = "08:00", horaFin = "17:00" } },
    };

    /// <summary>Primer día posterior al instante en que la zona adelanta el reloj a medianoche.</summary>
    internal static LocalDate ProximoDiaSinMedianoche(DateTimeZone zona, DateTime desdeUtc)
    {
        var desde = Instant.FromDateTimeUtc(DateTime.SpecifyKind(desdeUtc, DateTimeKind.Utc));
        var intervalos = zona.GetZoneIntervals(desde, desde + Duration.FromDays(800)).ToList();

        for (var i = 1; i < intervalos.Count; i++)
        {
            if (intervalos[i].WallOffset > intervalos[i - 1].WallOffset)
            {
                return intervalos[i].Start.InZone(zona).Date;
            }
        }

        throw new InvalidOperationException($"La zona {zona.Id} no adelanta el reloj en el periodo buscado.");
    }

    internal Task CambiarZonaEnBaseAsync(Guid companiaId, string zonaIana) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            var compania = await db.Set<Compania>().SingleAsync(c => c.Id == companiaId);
            compania.ZonaHorariaIana = zonaIana;
            await db.SaveChangesAsync();
        });

    private async Task<PermisoAcceso> FilaAsync(Guid id)
    {
        PermisoAcceso fila = null!;

        await fixture.Api.ConDbContextAsync(async db =>
            fila = await db.Set<PermisoAcceso>().AsNoTracking().SingleAsync(p => p.Id == id));

        return fila;
    }

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
