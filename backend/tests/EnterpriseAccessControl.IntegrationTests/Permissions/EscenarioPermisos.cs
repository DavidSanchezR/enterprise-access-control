using System.Net.Http.Json;
using EnterpriseAccessControl.Application.AreaAccess;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.AreaAccess;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Escenario compartido por las pruebas de la Historia 8.
/// </summary>
/// <remarks>
/// Se apoya en <see cref="EscenarioUs5"/> en lugar de duplicar su montaje: una evaluación de acceso
/// necesita exactamente lo que esa historia construye —pertenencia, contexto operativo, unidad,
/// perfil y credencial— más el área, sus tipos de persona autorizados y los permisos.
///
/// **La evaluación nunca se hace "ahora"**, sino en un instante fijo elegido dentro de las ventanas
/// de vigencia. Evaluar en el momento de ejecución haría que una prueba lanzada a las 23:59 cayera
/// fuera de cualquier bloque horario expresable en <c>HH:mm</c>, y el fallo parecería un defecto del
/// motor en lugar de un artefacto de la hora del reloj.
/// </remarks>
internal sealed class EscenarioPermisos(SqlServerFixture fixture)
{
    /// <summary>
    /// Instante evaluado: mañana a las 14:00 UTC, que en America/Lima son las 09:00 del mismo día.
    /// </summary>
    /// <remarks>
    /// Cae holgadamente dentro de las ventanas de <see cref="EscenarioUs5"/> (desde hace un mes hasta
    /// dentro de un año) y a una hora local cómoda para expresar bloques horarios.
    /// </remarks>
    public static readonly DateTime Instante =
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1).AddHours(14), DateTimeKind.Utc);

    private EscenarioUs5 _us5 = null!;

    public HttpClient Cliente => _us5.Cliente;

    public Persona Persona => _us5.Persona;

    public Compania PrincipalA => _us5.PrincipalA;

    public Compania PrincipalB => _us5.PrincipalB;

    public Compania Contratista => _us5.Contratista;

    public Guid TipoPersonaId { get; private set; }

    public AreaAccesoDto Area { get; private set; } = null!;

    /// <summary>Día de la semana local del instante evaluado, según el reloj real de la aplicación.</summary>
    public DiaSemana DiaEvaluado { get; private set; }

    /// <summary>
    /// Monta el caso que concede: persona de la Principal A con contexto, unidad, perfil y credencial
    /// vigentes, y un área de esa Principal que autoriza su perfil.
    /// </summary>
    /// <remarks>
    /// No crea ningún permiso: cada prueba decide cuáles existen, que es justo lo que quiere probar.
    /// </remarks>
    public async Task<EscenarioPermisos> MontarAsync(
        bool comoContratista = false,
        bool conCredencial = true)
    {
        _us5 = await new EscenarioUs5(fixture).MontarAsync();

        // La zona es la de la Principal propietaria del área evaluada (RF-080), no una del proceso.
        DiaEvaluado = ADiaSemana(RelojLocal().DiaSemanaLocal(Instante, PrincipalA.ZonaHorariaIana));

        var companiaPertenencia = comoContratista ? Contratista.Id : PrincipalA.Id;
        await _us5.PertenenciaVigenteAsync(companiaPertenencia);

        ContextoId = (await _us5.ContextoAsync(PrincipalA.Id)).Id;

        UnidadId = await _us5.SembrarUnidadAsync(PrincipalA.Id);
        (await _us5.AsignarUnidadAsync(ContextoId, UnidadId)).EnsureSuccessStatusCode();

        TipoPersonaId = await fixture.Api.SembrarTipoPersonaAsync();
        (await _us5.AsignarPerfilAsync(TipoPersonaId)).EnsureSuccessStatusCode();

        if (conCredencial)
        {
            await _us5.AsignarCredencialAsync(PrincipalA.Id);
        }

        Area =await CrearAreaAsync(PrincipalA.Id);
        await AutorizarPerfilEnAreaAsync(Area.Id, TipoPersonaId);

        return this;
    }

    public Guid ContextoId { get; private set; }

    public Guid UnidadId { get; private set; }

    public EscenarioUs5 Us5 => _us5;

    // --- Montaje ----------------------------------------------------------------------------------

    public async Task<AreaAccesoDto> CrearAreaAsync(Guid companiaPrincipalId, string nombre = "Planta")
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            new Uri("/api/areas-acceso", UriKind.Relative),
            new AreaAccesoRequest(
                $"{nombre} {EscenarioUs5.Sufijo()}", Estado.ACTIVO, null, companiaPrincipalId),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AreaAccesoDto>(ApiFactory.Json))!;
    }

    public async Task AutorizarPerfilEnAreaAsync(Guid areaId, params Guid[] tipoPersonaIds)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            new Uri($"/api/areas-acceso/{areaId}/tipos-persona", UriKind.Relative),
            new ReemplazarTiposPersonaRequest(tipoPersonaIds),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();
    }

    public async Task DesactivarAreaAsync(Guid areaId, string nombre)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            new Uri($"/api/areas-acceso/{areaId}", UriKind.Relative),
            new AreaAccesoUpdateRequest(nombre, Estado.INACTIVO),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();
    }

    // --- Permisos ----------------------------------------------------------------------------------

    /// <summary>Bloque horario que cubre el instante evaluado (09:00 local).</summary>
    public BloqueHorarioRequest BloqueQueCubre() => new(DiaEvaluado, "08:00", "17:00");

    /// <summary>
    /// 08:00–17:00 los siete días: para pruebas que evalúan en fechas distintas de <see cref="Instante"/>
    /// y no quieren que el día de la semana sea el factor que decide.
    /// </summary>
    public static IReadOnlyList<BloqueHorarioRequest> BloquesTodaLaSemana() =>
        [.. Enum.GetValues<DiaSemana>().Select(d => new BloqueHorarioRequest(d, "08:00", "17:00"))];

    /// <summary>Bloque horario del mismo día que **no** cubre el instante evaluado.</summary>
    public BloqueHorarioRequest BloqueQueNoCubre() => new(DiaEvaluado, "18:00", "22:00");

    /// <remarks>
    /// Desde <c>permissions.yaml</c> v2.0.0 (VF-004, RF-083) la vigencia viaja como fechas civiles. Por
    /// defecto, las fechas locales de la Principal A de <c>Instante − 1 mes</c> y <c>Instante + 6 meses</c>,
    /// que caben en la pertenencia de <see cref="EscenarioUs5"/>.
    /// </remarks>
    public PermisoAccesoRequest Peticion(
        AlcancePermiso alcance,
        Guid? sujetoId = null,
        Guid? areaId = null,
        DateOnly? inicio = null,
        DateOnly? fin = null,
        Estado estado = Estado.ACTIVO,
        IReadOnlyList<BloqueHorarioRequest>? bloques = null) =>
        new(
            areaId ?? Area.Id,
            alcance,
            inicio ?? FechaCivil(Instante.AddMonths(-1)),
            fin ?? FechaCivil(Instante.AddMonths(6)),
            estado,
            bloques ?? [BloqueQueCubre()],
            PersonaId: alcance == AlcancePermiso.PERSONA ? sujetoId ?? Persona.Id : null,
            UnidadOrganizativaId: alcance == AlcancePermiso.UNIDAD_ORGANIZATIVA
                ? sujetoId ?? UnidadId
                : null,
            CompaniaId: alcance == AlcancePermiso.COMPANIA ? sujetoId ?? PrincipalA.Id : null);

    /// <summary>Fecha civil de un instante en la zona de la Principal A (la de <see cref="Area"/>).</summary>
    public DateOnly FechaCivil(DateTime instanteUtc) =>
        Instant.FromDateTimeUtc(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc))
            .InZone(DateTimeZoneProviders.Tzdb[PrincipalA.ZonaHorariaIana ?? "America/Lima"])
            .Date
            .ToDateOnly();

    /// <summary>
    /// Fecha que declara una pertenencia: los componentes UTC de su instante normalizado.
    /// </summary>
    /// <remarks>
    /// Nunca se convierte a la zona del permiso: en Lima, las 00:00 UTC del primer día de la pertenencia
    /// caen en el día anterior (research.md §36.4).
    /// </remarks>
    public static DateOnly FechaDeclarada(DateTime instantePertenencia) => DateOnly.FromDateTime(instantePertenencia);

    public Task<HttpResponseMessage> PostPermisoAsync(PermisoAccesoRequest peticion) =>
        Cliente.PostAsJsonAsync(new Uri("/api/permisos", UriKind.Relative), peticion, ApiFactory.Json);

    public async Task<PermisoAccesoDto> CrearPermisoAsync(PermisoAccesoRequest peticion)
    {
        using var respuesta = await PostPermisoAsync(peticion);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<PermisoAccesoDto>(ApiFactory.Json))!;
    }

    public Task<PermisoAccesoDto> CrearPermisoAsync(
        AlcancePermiso alcance,
        Guid? sujetoId = null,
        IReadOnlyList<BloqueHorarioRequest>? bloques = null) =>
        CrearPermisoAsync(Peticion(alcance, sujetoId, bloques: bloques));

    // --- Evaluación --------------------------------------------------------------------------------

    public async Task<EvaluarAccesoResponse> EvaluarAsync(
        Guid? areaId = null,
        Guid? personaId = null,
        DateTime? fechaHora = null)
    {
        using var respuesta = await PostEvaluacionAsync(areaId, personaId, fechaHora);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<EvaluarAccesoResponse>(ApiFactory.Json))!;
    }

    public Task<HttpResponseMessage> PostEvaluacionAsync(
        Guid? areaId = null,
        Guid? personaId = null,
        DateTime? fechaHora = null,
        HttpClient? cliente = null) =>
        (cliente ?? Cliente).PostAsJsonAsync(
            new Uri("/api/evaluacion-acceso", UriKind.Relative),
            new EvaluarAccesoRequest(
                personaId ?? Persona.Id, areaId ?? Area.Id, fechaHora ?? Instante),
            ApiFactory.Json);

    // --- Utilidades --------------------------------------------------------------------------------

    private IRelojEmpresarial RelojLocal()
    {
        // El mismo componente que usa la aplicación: si la zona de la Principal cambiara, la prueba
        // se movería con ella en lugar de quedarse con un desfase escrito a mano.
        using var ambito = fixture.Api.Services.CreateScope();
        return ambito.ServiceProvider.GetRequiredService<IRelojEmpresarial>();
    }

    private static DiaSemana ADiaSemana(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => DiaSemana.LUNES,
        DayOfWeek.Tuesday => DiaSemana.MARTES,
        DayOfWeek.Wednesday => DiaSemana.MIERCOLES,
        DayOfWeek.Thursday => DiaSemana.JUEVES,
        DayOfWeek.Friday => DiaSemana.VIERNES,
        DayOfWeek.Saturday => DiaSemana.SABADO,
        DayOfWeek.Sunday => DiaSemana.DOMINGO,
        _ => throw new ArgumentOutOfRangeException(nameof(dia)),
    };
}
