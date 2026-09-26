using System.Globalization;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace EnterpriseAccessControl.Application.Permissions;

/// <summary>
/// Mantenimiento de permisos de acceso y sus bloques horarios
/// (Historia 8, RF-020, RF-021, RF-022, RF-039, RF-049).
/// </summary>
/// <remarks>
/// El alcance administrativo se evalúa contra el <c>CompaniaPrincipalId</c> del área referenciada y
/// no contra el sujeto del permiso (RF-049): lo que se está configurando es el acceso a un área, y
/// su dueña es quien debe autorizar el cambio. Un permiso puede otorgarse a una contratista que el
/// usuario no administra, siempre que el área sí lo esté.
///
/// Desde el cambio post-Baseline VF-007, un permiso de alcance PERSONA queda además contenido en la
/// pertenencia vigente de su persona cuando la escritura concede o amplía acceso (RF-082, D4; ver
/// <see cref="ReglaContencionPermiso"/>). Antes de consultar esa pertenencia se exige el alcance
/// histórico del usuario sobre la persona: consultarla es lo único nuevo que RF-082 expone, y sin esa
/// frontera sus errores revelarían datos de una compañía fuera del alcance (Principio I).
///
/// Desde el cambio post-Baseline VF-004 (RF-083), la vigencia se recibe como dos fechas civiles, cada una un
/// día completo en la zona efectiva de la Principal del área, y se convierte a los instantes UTC que se
/// persisten y evalúan (<see cref="VigenciaDiariaPermiso"/>). Esa zona solo se lee de un área que el usuario
/// administra. Al actualizar, cada extremo cuya fecha no cambia conserva su instante almacenado, así que los
/// permisos anteriores a VF-004 no se reinterpretan. La contención de RF-082 se compara por fecha civil
/// (research.md §36).
/// </remarks>
public sealed class PermisoAccesoService(
    IAppDbContext db,
    IAlcanceCompaniaAccessor alcance,
    PersonaService personas,
    ContencionTemporalValidator contencion,
    IRelojEmpresarial reloj)
{
    /// <summary>Formato de hora del contrato: <c>HH:mm</c> en hora local America/Lima.</summary>
    private const string FormatoHora = "HH\\:mm";

    public async Task<PaginaResponse<PermisoAccesoDto>> ListarAsync(
        FiltroPermisos filtro,
        ParametrosPaginacion paginacion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var consulta = EnAlcance(db.PermisosAcceso.AsNoTracking());

        if (filtro.AreaAccesoId is not null)
        {
            consulta = consulta.Where(p => p.AreaAccesoId == filtro.AreaAccesoId);
        }

        if (filtro.PersonaId is not null)
        {
            consulta = consulta.Where(p => p.PersonaId == filtro.PersonaId);
        }

        if (filtro.UnidadOrganizativaId is not null)
        {
            consulta = consulta.Where(p => p.UnidadOrganizativaId == filtro.UnidadOrganizativaId);
        }

        if (filtro.CompaniaId is not null)
        {
            consulta = consulta.Where(p => p.CompaniaId == filtro.CompaniaId);
        }

        if (filtro.Estado is not null)
        {
            consulta = consulta.Where(p => p.Estado == filtro.Estado);
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var permisos = await consulta
            .OrderBy(p => p.FechaHoraInicioVigencia)
            .ThenBy(p => p.Id)
            .Skip(paginacion.Saltar)
            .Take(paginacion.TamañoPagina)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var bloques = await BloquesDeAsync([.. permisos.Select(p => p.Id)], ct).ConfigureAwait(false);

        // Solo permisos ya filtrados por EnAlcance: ninguna zona sale de un área fuera de alcance.
        var zonas = await ZonasDeAsync([.. permisos.Select(p => p.AreaAccesoId).Distinct()], ct)
            .ConfigureAwait(false);

        return new PaginaResponse<PermisoAccesoDto>(
            [.. permisos.Select(p => AMapa(p, bloques, ZonaDe(zonas, p.AreaAccesoId)))],
            total,
            paginacion.Pagina,
            paginacion.TamañoPagina);
    }

    public async Task<PermisoAccesoDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var permiso = await ObtenerEnAlcanceAsync(id, seguimiento: false, ct).ConfigureAwait(false);
        var bloques = await BloquesDeAsync([permiso.Id], ct).ConfigureAwait(false);
        var zona = await ZonaDelAreaAsync(permiso.AreaAccesoId, ct).ConfigureAwait(false);

        return AMapa(permiso, bloques, zona);
    }

    public async Task<PermisoAccesoDto> CrearAsync(
        PermisoAccesoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (fechaInicio, fechaFin) = ValidarVigencia(request);
        var bloques = ValidarBloques(request.BloquesHorarios);

        // La zona se obtiene en la misma consulta que confirma el alcance sobre el área (Principio I).
        var zona = await ExigirAreaEnAlcanceAsync(request.AreaAccesoId, ct).ConfigureAwait(false);
        await ValidarSujetoAsync(request, ct).ConfigureAwait(false);

        // RF-083 (a): días civiles completos en la zona de la Principal del área, persistidos en UTC.
        var inicio = VigenciaDiariaPermiso.InicioUtc(fechaInicio, zona);
        var fin = VigenciaDiariaPermiso.FinUtc(fechaFin, zona);

        // RF-082: un alta PERSONA que queda ACTIVO debe caber en la pertenencia de su persona. Va después
        // de validar el sujeto, que conserva su 400 ante una persona inexistente.
        if (ReglaContencionPermiso.RequiereContencion(
                request.Alcance, estadoPrevio: null, request.Estado, fechasCambian: true))
        {
            await ContenerEnPertenenciaAsync(request.PersonaId!.Value, fechaInicio, fechaFin, inicio, fin, ct)
                .ConfigureAwait(false);
        }

        var permiso = new PermisoAcceso
        {
            AreaAccesoId = request.AreaAccesoId,
            Alcance = request.Alcance,
            PersonaId = request.Alcance == AlcancePermiso.PERSONA ? request.PersonaId : null,
            UnidadOrganizativaId = request.Alcance == AlcancePermiso.UNIDAD_ORGANIZATIVA
                ? request.UnidadOrganizativaId
                : null,
            CompaniaId = request.Alcance == AlcancePermiso.COMPANIA ? request.CompaniaId : null,
            FechaHoraInicioVigencia = inicio,
            FechaHoraFinVigencia = fin,
            Estado = request.Estado,
        };

        db.PermisosAcceso.Add(permiso);

        foreach (var (dia, desde, hasta) in bloques)
        {
            db.BloquesHorarioPermiso.Add(new BloqueHorarioPermiso
            {
                PermisoAccesoId = permiso.Id,
                DiaSemana = dia,
                HoraInicio = desde,
                HoraFin = hasta,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(permiso, await BloquesDeAsync([permiso.Id], ct).ConfigureAwait(false), zona);
    }

    /// <summary>
    /// Actualiza vigencia, estado y bloques horarios; el alcance y su sujeto no son editables.
    /// </summary>
    /// <remarks>
    /// Cambiar el sujeto de un permiso ya otorgado equivaldría a otorgar uno nuevo a alguien distinto
    /// conservando su identificador y su rastro de auditoría. El contrato lo enuncia en el propio
    /// resumen de la operación ("actualizar vigencia, estado y bloques horarios").
    /// </remarks>
    public async Task<PermisoAccesoDto> ActualizarAsync(
        Guid id,
        PermisoAccesoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (fechaInicio, fechaFin) = ValidarVigencia(request);
        var bloques = ValidarBloques(request.BloquesHorarios);

        var permiso = await ObtenerEnAlcanceAsync(id, seguimiento: true, ct).ConfigureAwait(false);

        if (request.Alcance != permiso.Alcance)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                "El alcance de un permiso no puede cambiarse; cree uno nuevo con el alcance deseado.");
        }

        // El área no es editable y el permiso ya está en alcance: su zona es la de un área visible.
        var zona = await ZonaDelAreaAsync(permiso.AreaAccesoId, ct).ConfigureAwait(false);

        // F-6: cada extremo por separado. Una fecha que no cambia conserva el instante almacenado, aunque sea
        // de un permiso anterior a VF-004 con hora; una que cambia se normaliza a su día completo.
        var inicio = VigenciaDiariaPermiso.ResolverExtremo(
            permiso.FechaHoraInicioVigencia, fechaInicio, zona, esFin: false);
        var fin = VigenciaDiariaPermiso.ResolverExtremo(
            permiso.FechaHoraFinVigencia, fechaFin, zona, esFin: true);

        // Con un extremo antiguo conservado, dos fechas válidas pueden dar instantes vacíos o invertidos (un
        // fin antiguo a las 00:00 locales y un inicio nuevo ese mismo día). No hay CHECK en base de datos que
        // lo impida, así que se rechaza aquí, antes de la contención y sin mutar nada (RF-039).
        if (fin.Instante <= inicio.Instante)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.PeriodoInvalido,
                "La fecha de fin de vigencia debe ser posterior a la de inicio.");
        }

        var fechasCambian = inicio.Cambia || fin.Cambia;

        // RF-082 D4: se valida antes de mutar nada, y solo si la escritura concede o amplía acceso
        // (cambia la vigencia o reactiva). Cambiar solo bloques o desactivar un permiso anterior a
        // RF-082 que excede la pertenencia sigue siendo posible, aunque la persona ya no pertenezca a
        // ninguna compañía (CS-043).
        if (ReglaContencionPermiso.RequiereContencion(
                permiso.Alcance, permiso.Estado, request.Estado, fechasCambian))
        {
            await ContenerEnPertenenciaAsync(
                    permiso.PersonaId!.Value, fechaInicio, fechaFin, inicio.Instante, fin.Instante, ct)
                .ConfigureAwait(false);
        }

        // Solo se escribe el extremo que cambió: el otro nunca se reescribe (F-6; RF-082 y RF-083 no son
        // retroactivas).
        if (inicio.Cambia)
        {
            permiso.FechaHoraInicioVigencia = inicio.Instante;
        }

        if (fin.Cambia)
        {
            permiso.FechaHoraFinVigencia = fin.Instante;
        }

        permiso.Estado = request.Estado;

        // El conjunto de bloques se reemplaza entero, igual que en el contrato: un horario es una
        // unidad ("lunes y miércoles de 08:00 a 17:00"), no una colección editable pieza a pieza.
        var actuales = await db.BloquesHorarioPermiso
            .Where(b => b.PermisoAccesoId == permiso.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        db.BloquesHorarioPermiso.RemoveRange(actuales);

        foreach (var (dia, desde, hasta) in bloques)
        {
            db.BloquesHorarioPermiso.Add(new BloqueHorarioPermiso
            {
                PermisoAccesoId = permiso.Id,
                DiaSemana = dia,
                HoraInicio = desde,
                HoraFin = hasta,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(permiso, await BloquesDeAsync([permiso.Id], ct).ConfigureAwait(false), zona);
    }

    // --- Validaciones ---------------------------------------------------------------------------

    /// <summary>
    /// RF-021, RF-039, RF-071, RF-083: vigencia completa, con fecha de fin igual o posterior a la de inicio.
    /// </summary>
    /// <remarks>
    /// La igualdad es válida: un permiso de un solo día cubre ese día completo (CS-045). Es una comprobación
    /// pura de la petición y va antes del alcance, como antes de VF-004.
    /// </remarks>
    private static (DateOnly Inicio, DateOnly Fin) ValidarVigencia(PermisoAccesoRequest request)
    {
        if (request.FechaFinVigencia < request.FechaInicioVigencia)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.PeriodoInvalido,
                "La fecha de fin de vigencia no puede ser anterior a la de inicio.");
        }

        return (request.FechaInicioVigencia, request.FechaFinVigencia);
    }

    /// <summary>
    /// RF-022, RF-039: cada bloque con fin posterior al inicio y sin solaparse con otro del mismo día.
    /// </summary>
    private static List<(DiaSemana Dia, TimeOnly Inicio, TimeOnly Fin)> ValidarBloques(
        IReadOnlyList<BloqueHorarioRequest> solicitados)
    {
        if (solicitados is null || solicitados.Count == 0)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                "El permiso requiere al menos un bloque horario (RF-022).");
        }

        var bloques = new List<(DiaSemana Dia, TimeOnly Inicio, TimeOnly Fin)>(solicitados.Count);

        foreach (var solicitado in solicitados)
        {
            var inicio = AHora(solicitado.HoraInicio, nameof(BloqueHorarioRequest.HoraInicio));
            var fin = AHora(solicitado.HoraFin, nameof(BloqueHorarioRequest.HoraFin));

            if (fin <= inicio)
            {
                throw new ReglaNegocioInvalidaException(
                    CodigosError.PeriodoInvalido,
                    $"El bloque de {solicitado.DiaSemana} termina antes de empezar ({solicitado.HoraInicio}–{solicitado.HoraFin}).");
            }

            // Comparación de fin exclusiva: 08:00–12:00 y 12:00–17:00 son consecutivos, no solapados.
            var choca = bloques.Exists(b =>
                b.Dia == solicitado.DiaSemana && b.Inicio < fin && inicio < b.Fin);

            if (choca)
            {
                throw new ReglaNegocioInvalidaException(
                    CodigosError.SolapamientoVigencia,
                    $"Dos bloques horarios del mismo día ({solicitado.DiaSemana}) se solapan.");
            }

            bloques.Add((solicitado.DiaSemana, inicio, fin));
        }

        return bloques;
    }

    /// <summary>
    /// Exactamente un sujeto informado, coincidente con el alcance, y existente.
    /// </summary>
    /// <remarks>
    /// Una compañía de alcance COMPAÑÍA puede ser PRINCIPAL_MANDANTE o CONTRATISTA indistintamente
    /// (research.md §12): dar acceso a todo el personal de una contratista sobre un área de la
    /// Principal a la que presta servicios es un caso legítimo.
    /// </remarks>
    private async Task ValidarSujetoAsync(PermisoAccesoRequest request, CancellationToken ct)
    {
        var informados = new[] { request.PersonaId, request.UnidadOrganizativaId, request.CompaniaId }
            .Count(id => id is not null);

        if (informados != 1)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                "Debe informarse exactamente uno de personaId, unidadOrganizativaId o companiaId.");
        }

        var existe = request.Alcance switch
        {
            AlcancePermiso.PERSONA when request.PersonaId is not null =>
                await db.Personas.AnyAsync(p => p.Id == request.PersonaId, ct).ConfigureAwait(false),

            AlcancePermiso.UNIDAD_ORGANIZATIVA when request.UnidadOrganizativaId is not null =>
                await db.UnidadesOrganizativas
                    .AnyAsync(u => u.Id == request.UnidadOrganizativaId, ct)
                    .ConfigureAwait(false),

            AlcancePermiso.COMPANIA when request.CompaniaId is not null =>
                await db.Companias.AnyAsync(c => c.Id == request.CompaniaId, ct).ConfigureAwait(false),

            // El sujeto informado no es el que corresponde al alcance declarado.
            _ => throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                $"El sujeto informado no corresponde al alcance {request.Alcance}."),
        };

        if (!existe)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                $"El sujeto indicado para el alcance {request.Alcance} no existe.");
        }
    }

    /// <summary>
    /// Contención de RF-082 para un permiso PERSONA, precedida de la frontera de alcance sobre la persona.
    /// </summary>
    /// <remarks>
    /// El orden es obligatorio (research.md §35.6): primero el alcance histórico del usuario sobre la
    /// persona —el mismo control que protege el alta de perfil—, que produce 404 si está fuera; solo
    /// después se consulta su pertenencia. Así, SIN_PERTENENCIA_VIGENTE y FUERA_DE_CONTENCION_TEMPORAL
    /// nunca informan sobre una persona que el usuario no administra. La autorización y la contención
    /// son las de siempre: aquí solo se decide su orden.
    ///
    /// Desde VF-004 la contención se compara por fecha civil (RF-083 (c), F-2). El método recibe además los
    /// instantes UTC ya resueltos de la vigencia. La regla no los usa, porque compararlos rechazaría el mismo
    /// día por la diferencia de representación UTC. Se reciben para que la regresión dirigida de T310 (b)
    /// pueda sustituir solo esta llamada por la comparación por instantes.
    /// </remarks>
    private async Task ContenerEnPertenenciaAsync(
        Guid personaId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        DateTime inicioUtc,
        DateTime finUtc,
        CancellationToken ct)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);
        await contencion.ValidarFechasCivilesAsync(personaId, fechaInicio, fechaFin, ct).ConfigureAwait(false);
    }

    // --- Alcance administrativo (RF-049) --------------------------------------------------------

    /// <summary>Restringe la consulta a los permisos de áreas cuya Principal el usuario administra.</summary>
    /// <remarks>
    /// Un GLOBAL_ADMINISTRATOR no enumera compañías (RF-074): filtrar por su conjunto vacío lo
    /// dejaría sin ver ningún permiso, que es lo contrario de su alcance.
    /// </remarks>
    private IQueryable<PermisoAcceso> EnAlcance(IQueryable<PermisoAcceso> consulta)
    {
        if (alcance.EsGlobal)
        {
            return consulta;
        }

        var companias = alcance.CompaniaIds.ToList();

        return consulta.Where(p => db.AreasAcceso
            .Any(a => a.Id == p.AreaAccesoId && companias.Contains(a.CompaniaPrincipalId)));
    }

    /// <summary>
    /// Exige que el área esté en el alcance del usuario y devuelve la zona efectiva de su Principal.
    /// </summary>
    /// <remarks>
    /// La zona sale de la <b>misma</b> consulta que confirma el alcance: de un área fuera de alcance no se lee
    /// nada, y el resultado sigue siendo el 404 de siempre (Principio I, RF-049; research.md §36.2).
    /// </remarks>
    private async Task<DateTimeZone> ExigirAreaEnAlcanceAsync(Guid areaAccesoId, CancellationToken ct)
    {
        var companias = alcance.CompaniaIds.ToList();

        var visible = await db.AreasAcceso
            .AsNoTracking()
            .Where(a => a.Id == areaAccesoId
                        && (alcance.EsGlobal || companias.Contains(a.CompaniaPrincipalId)))
            .Select(a => new
            {
                Zona = db.Companias
                    .Where(c => c.Id == a.CompaniaPrincipalId)
                    .Select(c => c.ZonaHorariaIana)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (visible is null)
        {
            // Fuera de alcance es indistinguible de inexistente (Principio I, RF-049).
            throw NoEncontrado();
        }

        return ZonaEfectiva(visible.Zona);
    }

    /// <summary>Zona efectiva del área de un permiso ya obtenido dentro del alcance.</summary>
    private async Task<DateTimeZone> ZonaDelAreaAsync(Guid areaAccesoId, CancellationToken ct)
    {
        var zonas = await ZonasDeAsync([areaAccesoId], ct).ConfigureAwait(false);
        return ZonaDe(zonas, areaAccesoId);
    }

    /// <summary>
    /// Zonas efectivas de las áreas de una página de permisos ya filtrada por <see cref="EnAlcance"/>.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, DateTimeZone>> ZonasDeAsync(
        List<Guid> areaIds,
        CancellationToken ct)
    {
        if (areaIds.Count == 0)
        {
            return new Dictionary<Guid, DateTimeZone>();
        }

        var filas = await db.AreasAcceso
            .AsNoTracking()
            .Where(a => areaIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                Zona = db.Companias
                    .Where(c => c.Id == a.CompaniaPrincipalId)
                    .Select(c => c.ZonaHorariaIana)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return filas.ToDictionary(f => f.Id, f => ZonaEfectiva(f.Zona));
    }

    /// <summary>
    /// La misma zona que usa el paso 13 de la evaluación: la de la Principal o, si no es utilizable, la global
    /// de respaldo (RF-080, RF-083 (a)).
    /// </summary>
    private DateTimeZone ZonaEfectiva(string? zonaIana) =>
        VigenciaDiariaPermiso.Zona(reloj.ZonaEfectiva(zonaIana));

    private DateTimeZone ZonaDe(IReadOnlyDictionary<Guid, DateTimeZone> zonas, Guid areaAccesoId) =>
        zonas.TryGetValue(areaAccesoId, out var zona) ? zona : ZonaEfectiva(null);

    private async Task<PermisoAcceso> ObtenerEnAlcanceAsync(
        Guid id,
        bool seguimiento,
        CancellationToken ct)
    {
        var consulta = seguimiento ? db.PermisosAcceso : db.PermisosAcceso.AsNoTracking();

        return await EnAlcance(consulta).FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
            ?? throw NoEncontrado();
    }

    // --- Conversión ------------------------------------------------------------------------------

    private async Task<ILookup<Guid, BloqueHorarioPermiso>> BloquesDeAsync(
        List<Guid> permisoIds,
        CancellationToken ct)
    {
        if (permisoIds.Count == 0)
        {
            return Array.Empty<BloqueHorarioPermiso>().ToLookup(b => b.PermisoAccesoId);
        }

        var bloques = await db.BloquesHorarioPermiso
            .AsNoTracking()
            .Where(b => permisoIds.Contains(b.PermisoAccesoId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return bloques.ToLookup(b => b.PermisoAccesoId);
    }


    private static TimeOnly AHora(string valor, string campo)
    {
        if (!TimeOnly.TryParseExact(
                valor,
                FormatoHora,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var hora))
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                $"{campo} debe expresarse como HH:mm en hora local (recibido: '{valor}').");
        }

        return hora;
    }

    /// <remarks>
    /// Las fechas civiles, <c>VigenciaEnDiasCompletos</c> y la zona se calculan en cada lectura con la zona
    /// efectiva actual y no se persisten: cambiar la zona de la Principal no modifica ninguna fila, solo esta
    /// representación (RF-083 (f), (h); F-5, F-7).
    /// </remarks>
    private static PermisoAccesoDto AMapa(
        PermisoAcceso permiso,
        ILookup<Guid, BloqueHorarioPermiso> bloques,
        DateTimeZone zona) =>
        new(
            permiso.Id,
            permiso.AreaAccesoId,
            permiso.Alcance,
            permiso.PersonaId,
            permiso.UnidadOrganizativaId,
            permiso.CompaniaId,
            permiso.FechaHoraInicioVigencia,
            permiso.FechaHoraFinVigencia,
            VigenciaDiariaPermiso.FechaCivil(permiso.FechaHoraInicioVigencia, zona),
            VigenciaDiariaPermiso.FechaCivil(permiso.FechaHoraFinVigencia, zona),
            VigenciaDiariaPermiso.EsDiaCompleto(
                permiso.FechaHoraInicioVigencia, permiso.FechaHoraFinVigencia, zona),
            zona.Id,
            permiso.Estado,
            [.. bloques[permiso.Id]
                .OrderBy(b => b.DiaSemana)
                .ThenBy(b => b.HoraInicio)
                .Select(b => new BloqueHorarioDto(
                    b.Id,
                    b.DiaSemana,
                    b.HoraInicio.ToString(FormatoHora, CultureInfo.InvariantCulture),
                    b.HoraFin.ToString(FormatoHora, CultureInfo.InvariantCulture)))]);

    private static RecursoNoEncontradoException NoEncontrado() =>
        new(CodigosError.RecursoNoEncontrado, "Permiso de acceso no encontrado.");
}
