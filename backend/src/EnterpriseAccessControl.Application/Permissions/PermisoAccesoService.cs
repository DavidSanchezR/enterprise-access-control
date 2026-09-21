using System.Globalization;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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
/// </remarks>
public sealed class PermisoAccesoService(IAppDbContext db, IAlcanceCompaniaAccessor alcance)
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

        return new PaginaResponse<PermisoAccesoDto>(
            [.. permisos.Select(p => AMapa(p, bloques))],
            total,
            paginacion.Pagina,
            paginacion.TamañoPagina);
    }

    public async Task<PermisoAccesoDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var permiso = await ObtenerEnAlcanceAsync(id, seguimiento: false, ct).ConfigureAwait(false);
        var bloques = await BloquesDeAsync([permiso.Id], ct).ConfigureAwait(false);

        return AMapa(permiso, bloques);
    }

    public async Task<PermisoAccesoDto> CrearAsync(
        PermisoAccesoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (inicio, fin) = ValidarVigencia(request);
        var bloques = ValidarBloques(request.BloquesHorarios);

        await ExigirAreaEnAlcanceAsync(request.AreaAccesoId, ct).ConfigureAwait(false);
        await ValidarSujetoAsync(request, ct).ConfigureAwait(false);

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

        return AMapa(permiso, await BloquesDeAsync([permiso.Id], ct).ConfigureAwait(false));
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

        var (inicio, fin) = ValidarVigencia(request);
        var bloques = ValidarBloques(request.BloquesHorarios);

        var permiso = await ObtenerEnAlcanceAsync(id, seguimiento: true, ct).ConfigureAwait(false);

        if (request.Alcance != permiso.Alcance)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                "El alcance de un permiso no puede cambiarse; cree uno nuevo con el alcance deseado.");
        }

        permiso.FechaHoraInicioVigencia = inicio;
        permiso.FechaHoraFinVigencia = fin;
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

        return AMapa(permiso, await BloquesDeAsync([permiso.Id], ct).ConfigureAwait(false));
    }

    // --- Validaciones ---------------------------------------------------------------------------

    /// <summary>RF-021, RF-039, RF-071: vigencia completa y con fin posterior al inicio.</summary>
    private static (DateTime Inicio, DateTime Fin) ValidarVigencia(PermisoAccesoRequest request)
    {
        var inicio = InstanteUtc.Desde(request.FechaHoraInicioVigencia);
        var fin = InstanteUtc.Desde(request.FechaHoraFinVigencia);

        if (fin <= inicio)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.PeriodoInvalido,
                "La fecha de fin de vigencia debe ser posterior a la de inicio.");
        }

        return (inicio, fin);
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

    private async Task ExigirAreaEnAlcanceAsync(Guid areaAccesoId, CancellationToken ct)
    {
        var companias = alcance.CompaniaIds.ToList();

        var visible = await db.AreasAcceso
            .AsNoTracking()
            .AnyAsync(
                a => a.Id == areaAccesoId
                     && (alcance.EsGlobal || companias.Contains(a.CompaniaPrincipalId)),
                ct)
            .ConfigureAwait(false);

        if (!visible)
        {
            // Fuera de alcance es indistinguible de inexistente (Principio I, RF-049).
            throw NoEncontrado();
        }
    }

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

    private static PermisoAccesoDto AMapa(
        PermisoAcceso permiso,
        ILookup<Guid, BloqueHorarioPermiso> bloques) =>
        new(
            permiso.Id,
            permiso.AreaAccesoId,
            permiso.Alcance,
            permiso.PersonaId,
            permiso.UnidadOrganizativaId,
            permiso.CompaniaId,
            permiso.FechaHoraInicioVigencia,
            permiso.FechaHoraFinVigencia,
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
