using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Reconstrucción del estado efectivo de una persona en una fecha/hora dada (RF-037).
/// </summary>
/// <remarks>
/// Responde "¿cómo estaba esta persona el día X?" a partir del histórico, sin depender de ningún
/// campo de estado: todo se deriva comparando las ventanas de vigencia contra la fecha evaluada
/// (Principio IV). Eso permite reconstruir correctamente también fechas pasadas, en las que los
/// campos <c>Estado</c> actuales ya no describen la situación de entonces.
///
/// Devuelve **todos** los contextos vigentes, no uno: una persona puede operar con varias Compañías
/// Principales a la vez (RF-052, CS-013).
/// </remarks>
public sealed class EstadoEfectivoService(
    IAppDbContext db,
    PersonaService personas,
    ContencionTemporalValidator contencion)
{
    public async Task<EstadoEfectivoPersonaDto> ObtenerAsync(
        Guid personaId,
        DateTime fechaHora,
        CancellationToken ct = default)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var companiaVigenteId = await db.AsignacionesPersonaCompania
            .AsNoTracking()
            .Where(a => a.PersonaId == personaId
                        && a.FechaHoraInicio <= fechaHora
                        && fechaHora < a.FechaHoraFin)
            .OrderByDescending(a => a.FechaHoraInicio)
            .Select(a => (Guid?)a.CompaniaId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var contextos = await db.ContextosOperativos
            .AsNoTracking()
            .Where(c => c.PersonaId == personaId
                        && c.FechaHoraInicio <= fechaHora
                        && fechaHora < c.FechaHoraFin)
            .Select(c => new { c.Id, c.CompaniaPrincipalId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var contextoIds = contextos.ConvertAll(c => c.Id);

        // Las unidades vigentes se traen en una sola consulta para toda la lista de contextos, en
        // lugar de una por contexto.
        var unidades = await db.AsignacionesUnidadOrganizativa
            .AsNoTracking()
            .Where(u => contextoIds.Contains(u.ContextoOperativoId)
                        && u.FechaHoraInicio <= fechaHora
                        && fechaHora < u.FechaHoraFin)
            .Select(u => new { u.ContextoOperativoId, u.UnidadOrganizativaId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var contextosVigentes = contextos.ConvertAll(c => new ContextoOperativoVigenteDto(
            c.Id,
            c.CompaniaPrincipalId,
            unidades.Find(u => u.ContextoOperativoId == c.Id)?.UnidadOrganizativaId));

        var perfiles = await db.AsignacionesTipoPersona
            .AsNoTracking()
            .Where(p => p.PersonaId == personaId
                        && p.FechaHoraInicio <= fechaHora
                        && fechaHora < p.FechaHoraFin)
            .Select(p => p.TipoPersonaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new EstadoEfectivoPersonaDto(
            personaId, fechaHora, companiaVigenteId, contextosVigentes, perfiles);
    }

    /// <summary>Perfiles de la persona; admite varios vigentes a la vez, sin exclusividad (RF-011).</summary>
    public async Task<IReadOnlyList<AsignacionTipoPersonaDto>> ListarPerfilesAsync(
        Guid personaId,
        CancellationToken ct = default)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        return await db.AsignacionesTipoPersona
            .AsNoTracking()
            .Where(p => p.PersonaId == personaId)
            .OrderByDescending(p => p.FechaHoraInicio)
            .Select(p => new AsignacionTipoPersonaDto(
                p.Id, p.PersonaId, p.TipoPersonaId, p.FechaHoraInicio, p.FechaHoraFin, p.Estado))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asigna un perfil. Sin exclusividad (RF-011), pero contenido en la pertenencia vigente (RF-082).
    /// </summary>
    /// <remarks>
    /// Cambio post-Baseline VF-007: el perfil pasa a estar sujeto a la contención de RF-072, pero no a la
    /// cascada de RF-061. Cerrar la pertenencia no lo revoca; solo impide registrar perfiles que la
    /// excedan. El alcance se exige antes de consultar la pertenencia, así que una persona fuera de
    /// alcance produce 404 y nunca un error que revele su pertenencia (Principio I).
    /// </remarks>
    public async Task<AsignacionTipoPersonaDto> AsignarPerfilAsync(
        Guid personaId,
        AsignacionTipoPersonaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var (inicio, fin) = Vigencia.NormalizarRango(request.FechaHoraInicio, request.FechaHoraFin);

        // Mismo orden que el contexto operativo: normalizar → contener → reglas propias.
        await contencion.ValidarAsync(personaId, inicio, fin, ct).ConfigureAwait(false);

        var activo = await db.Maestro<TipoPersona>()
            .AnyAsync(t => t.Id == request.TipoPersonaId && t.Estado == Estado.ACTIVO, ct)
            .ConfigureAwait(false);

        if (!activo)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValorMaestroInactivo,
                "El tipo de persona indicado no existe o está inactivo.");
        }

        var asignacion = new AsignacionTipoPersona
        {
            PersonaId = personaId,
            TipoPersonaId = request.TipoPersonaId,
            FechaHoraInicio = inicio,
            FechaHoraFin = fin,
            Estado = Estado.ACTIVO,
        };

        db.AsignacionesTipoPersona.Add(asignacion);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new AsignacionTipoPersonaDto(
            asignacion.Id, asignacion.PersonaId, asignacion.TipoPersonaId,
            asignacion.FechaHoraInicio, asignacion.FechaHoraFin, asignacion.Estado);
    }
}
