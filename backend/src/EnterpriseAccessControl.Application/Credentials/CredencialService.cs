using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Credentials;

/// <summary>contracts/credentials.yaml — AsignacionCredencialRequest.</summary>
/// <remarks>La persona viaja en la ruta, no en el cuerpo.</remarks>
public sealed record AsignacionCredencialRequest(
    Guid CompaniaPrincipalId,
    Guid TipoCredencialId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin);

/// <summary>contracts/credentials.yaml — AsignacionCredencial.</summary>
public sealed record AsignacionCredencialDto(
    Guid Id,
    Guid PersonaId,
    Guid CompaniaPrincipalId,
    Guid TipoCredencialId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin,
    EstadoCredencial Estado,
    Guid? RevocadoPorPertenenciaId);

/// <summary>
/// Credenciales de una persona en el contexto de cada Compañía Principal
/// (Historia 9; RF-018, RF-056, RF-057, RF-072).
/// </summary>
/// <remarks>
/// La credencial se emite **dentro de un contexto operativo vigente** con esa misma Principal
/// (RF-056): una credencial sin contexto que la sustente no representaría ninguna autorización real.
///
/// <c>ASIGNADO</c> es el único estado no terminal (contracts/credentials.yaml). Devolver y dar de baja
/// solo parten de él, y ninguna operación borra filas: el histórico se conserva (RF-018). La cascada
/// de revocación (<c>REVOCADA</c>) vive en <see cref="RevocacionService"/> y no se expone aquí.
///
/// A diferencia de pertenencia, contexto y unidad organizativa, asignar una credencial **no** cierra la
/// anterior (Sesión 2026-09-15, decisión A): cada estado terminal tiene una única causa y ninguna es un
/// reemplazo, así que un solapamiento se rechaza en lugar de resolverse.
///
/// Los códigos de error siguen literalmente contracts/credentials.yaml: la falta de contexto o una
/// compañía que no es Principal son 400; un tipo de credencial inactivo, el solapamiento con otra
/// credencial ASIGNADO y el exceso sobre la pertenencia (RF-072) son 409.
/// </remarks>
public sealed class CredencialService(
    IAppDbContext db,
    ContencionTemporalValidator contencion,
    PersonaService personas,
    IAlcanceCompaniaAccessor alcance,
    IRelojSistema reloj)
{
    /// <summary>Histórico de credenciales, del inicio más reciente al más antiguo (RF-018).</summary>
    public async Task<IReadOnlyList<AsignacionCredencialDto>> ListarAsync(
        Guid personaId,
        Guid? companiaPrincipalId,
        CancellationToken ct = default)
    {
        // Lectura de histórico: alcanza también a personas cuya pertenencia ya terminó (RF-037).
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var consulta = db.AsignacionesCredencial
            .AsNoTracking()
            .Where(c => c.PersonaId == personaId);

        if (companiaPrincipalId is not null)
        {
            consulta = consulta.Where(c => c.CompaniaPrincipalId == companiaPrincipalId);
        }

        return await consulta
            .OrderByDescending(c => c.FechaHoraInicio)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new AsignacionCredencialDto(
                c.Id, c.PersonaId, c.CompaniaPrincipalId, c.TipoCredencialId,
                c.FechaHoraInicio, c.FechaHoraFin, c.Estado, c.RevocadoPorPertenenciaId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<AsignacionCredencialDto> AsignarAsync(
        Guid personaId,
        AsignacionCredencialRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var (inicio, fin) = Vigencia.NormalizarRango(request.FechaHoraInicio, request.FechaHoraFin);

        await ExigirPrincipalAsync(request.CompaniaPrincipalId, ct).ConfigureAwait(false);

        await contencion.ValidarAsync(personaId, inicio, fin, ct).ConfigureAwait(false);

        var ahora = reloj.UtcNow;

        var hayContexto = await db.ContextosOperativos
            .AsNoTracking()
            .AnyAsync(
                c => c.PersonaId == personaId
                     && c.CompaniaPrincipalId == request.CompaniaPrincipalId
                     && c.Estado == Estado.ACTIVO
                     && c.FechaHoraInicio <= ahora
                     && ahora < c.FechaHoraFin,
                ct)
            .ConfigureAwait(false);

        if (!hayContexto)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.SinContextoOperativoVigente,
                "La persona no tiene un contexto operativo vigente con esa Compañía Principal (RF-056).");
        }

        var tipoActivo = await db.Maestro<TipoCredencial>()
            .AnyAsync(t => t.Id == request.TipoCredencialId && t.Estado == Estado.ACTIVO, ct)
            .ConfigureAwait(false);

        if (!tipoActivo)
        {
            throw new ConflictoEstadoException(
                CodigosError.ValorMaestroInactivo,
                "El tipo de credencial indicado no existe o está inactivo.");
        }

        // RF-057, decisión A (Sesión 2026-09-15): un solapamiento con otra credencial ASIGNADO de la misma
        // persona y Principal se rechaza, y la previa NO se cierra ni se modifica. La comprobación usa la
        // misma condición que el trigger, que queda como última defensa ante escrituras concurrentes
        // (research.md §5).
        var seSolapa = await db.AsignacionesCredencial
            .AsNoTracking()
            .AnyAsync(
                c => c.PersonaId == personaId
                     && c.CompaniaPrincipalId == request.CompaniaPrincipalId
                     && c.Estado == EstadoCredencial.ASIGNADO
                     && c.FechaHoraInicio < fin
                     && inicio < c.FechaHoraFin,
                ct)
            .ConfigureAwait(false);

        if (seSolapa)
        {
            throw new ConflictoEstadoException(
                CodigosError.SolapamientoVigencia,
                "La persona ya tiene una credencial ASIGNADO para esa Compañía Principal que se solapa con el período indicado (RF-057).");
        }

        var credencial = new AsignacionCredencial
        {
            PersonaId = personaId,
            CompaniaPrincipalId = request.CompaniaPrincipalId,
            TipoCredencialId = request.TipoCredencialId,
            FechaHoraInicio = inicio,
            FechaHoraFin = fin,
            Estado = EstadoCredencial.ASIGNADO,
        };

        db.AsignacionesCredencial.Add(credencial);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(credencial);
    }

    /// <summary>Devolución física de la credencial: pasa a DEVUELTO y cierra su vigencia.</summary>
    public Task DevolverAsync(Guid personaId, Guid credencialId, CancellationToken ct = default) =>
        CerrarAsync(personaId, credencialId, EstadoCredencial.DEVUELTO, ct);

    /// <summary>
    /// Baja lógica administrativa: pasa a ELIMINADO y cierra su vigencia; la fila nunca se borra.
    /// </summary>
    public Task EliminarAsync(Guid personaId, Guid credencialId, CancellationToken ct = default) =>
        CerrarAsync(personaId, credencialId, EstadoCredencial.ELIMINADO, ct);

    /// <summary>
    /// Cierre administrativo común a devolver y dar de baja.
    /// </summary>
    /// <remarks>
    /// data-model.md: al cerrarse administrativamente, <c>FechaHoraFin</c> "se ajusta al valor
    /// efectivo de cierre, sin extenderla más allá de lo ya declarado". El valor efectivo es el
    /// instante de la operación, y se aplica la misma regla que la cascada
    /// (<see cref="CierreDeVigencia.Acortar"/>): la menor de ambas fechas.
    /// <c>FechaHoraInicio</c>, la Principal y el tipo no se tocan (RF-063).
    /// </remarks>
    private async Task CerrarAsync(
        Guid personaId,
        Guid credencialId,
        EstadoCredencial estadoFinal,
        CancellationToken ct)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var credencial = await db.AsignacionesCredencial
            .FirstOrDefaultAsync(c => c.Id == credencialId && c.PersonaId == personaId, ct)
            .ConfigureAwait(false);

        // Una credencial de otra persona, o de una Principal fuera del alcance, es indistinguible de
        // una inexistente (Principio I).
        if (credencial is null || !alcance.EstaEnAlcance(credencial.CompaniaPrincipalId))
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado, "Credencial no encontrada.");
        }

        if (credencial.Estado != EstadoCredencial.ASIGNADO)
        {
            throw new ConflictoEstadoException(
                CodigosError.CredencialNoAsignada,
                $"La credencial está en estado {credencial.Estado}; solo una credencial ASIGNADO puede cerrarse.");
        }

        CierreDeVigencia.Cerrar(credencial, reloj.UtcNow, estadoFinal, revocadoPorPertenenciaId: null);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// La Principal debe estar en alcance, existir y ser PRINCIPAL_MANDANTE (RF-056, data-model.md).
    /// </summary>
    /// <remarks>Mismo criterio que la apertura de contexto operativo.</remarks>
    private async Task ExigirPrincipalAsync(Guid companiaPrincipalId, CancellationToken ct)
    {
        if (!alcance.EstaEnAlcance(companiaPrincipalId))
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado, "Compañía principal no encontrada.");
        }

        var principal = await db.Companias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companiaPrincipalId, ct)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado, "Compañía principal no encontrada.");

        if (principal.TipoCompania != TipoCompania.PRINCIPAL_MANDANTE)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.CompaniaDebeSerPrincipal,
                "La credencial debe emitirse para una compañía PRINCIPAL_MANDANTE (RF-056).");
        }
    }

    internal static AsignacionCredencialDto AMapa(AsignacionCredencial c) =>
        new(c.Id, c.PersonaId, c.CompaniaPrincipalId, c.TipoCredencialId,
            c.FechaHoraInicio, c.FechaHoraFin, c.Estado, c.RevocadoPorPertenenciaId);
}
