using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Asignación de una persona a una unidad organizativa dentro de un contexto operativo
/// (RF-015, RF-055, RF-072).
/// </summary>
/// <remarks>
/// La exclusividad es **por contexto**: la misma persona puede tener una unidad vigente en cada uno
/// de sus contextos simultáneos (CS-014). Asignar una unidad nueva en un contexto cierra la anterior
/// de ese mismo contexto, y no toca las de los demás.
///
/// La unidad debe pertenecer al árbol de la misma Compañía Principal del contexto (RF-055): asignar
/// a alguien una unidad de otra Principal le daría una ubicación organizativa que su contexto no
/// justifica.
/// </remarks>
public sealed class AsignacionUnidadOrganizativaService(
    IAppDbContext db,
    ContencionTemporalValidator contencion,
    IJerarquiaConsultas jerarquia,
    PersonaService personas,
    IRelojSistema reloj)
{
    public async Task<AsignacionUnidadOrganizativaDto> AsignarAsync(
        Guid personaId,
        Guid contextoId,
        AsignacionUnidadOrganizativaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var (inicio, fin) = Vigencia.NormalizarRango(request.FechaHoraInicio, request.FechaHoraFin);

        var contexto = await db.ContextosOperativos
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contextoId && c.PersonaId == personaId, ct)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado, "Contexto operativo no encontrado.");

        if (!contexto.EstaVigenteEn(reloj.UtcNow) || contexto.Estado != Estado.ACTIVO)
        {
            throw new ConflictoEstadoException(
                "CONTEXTO_NO_VIGENTE",
                "El contexto operativo no está vigente; no admite nuevas asignaciones.");
        }

        await contencion.ValidarAsync(personaId, inicio, fin, ct).ConfigureAwait(false);

        await ValidarUnidadDeLaPrincipalAsync(request.UnidadOrganizativaId, contexto, ct)
            .ConfigureAwait(false);

        var asignacion = new AsignacionPersonaUnidadOrganizativa
        {
            PersonaId = personaId,
            ContextoOperativoId = contextoId,
            UnidadOrganizativaId = request.UnidadOrganizativaId,
            FechaHoraInicio = inicio,
            FechaHoraFin = fin,
            Estado = Estado.ACTIVO,
        };

        await db.EjecutarEnTransaccionAsync(
            async cancelacion =>
            {
                var previa = await db.AsignacionesUnidadOrganizativa
                    .Where(a => a.ContextoOperativoId == contextoId
                                && a.Estado == Estado.ACTIVO
                                && a.FechaHoraFin > inicio)
                    .OrderByDescending(a => a.FechaHoraInicio)
                    .FirstOrDefaultAsync(cancelacion)
                    .ConfigureAwait(false);

                if (previa is not null)
                {
                    if (inicio <= previa.FechaHoraInicio)
                    {
                        throw new ConflictoEstadoException(
                            CodigosError.SolapamientoVigencia,
                            "La nueva asignación debe iniciar después del inicio de la vigente en este contexto.");
                    }

                    // Cierre por reemplazo: termina justo antes de que empiece la nueva. No es una
                    // revocación en cascada, así que no se registra pertenencia de origen.
                    CierreDeVigencia.Cerrar(
                        previa,
                        inicio.AddMilliseconds(-1),
                        MotivoFinRevocacion.REEMPLAZO_ASIGNACION,
                        revocadoPorPertenenciaId: null);

                    // El cierre se confirma antes del alta: el trigger se evalúa por sentencia y
                    // vería ambas vigentes si el INSERT se adelantara.
                    await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                }

                db.AsignacionesUnidadOrganizativa.Add(asignacion);
                await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            },
            ct)
            .ConfigureAwait(false);

        return AMapa(asignacion);
    }

    public async Task<IReadOnlyList<AsignacionUnidadOrganizativaDto>> ListarAsync(
        Guid personaId,
        Guid contextoId,
        CancellationToken ct = default)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        return await db.AsignacionesUnidadOrganizativa
            .AsNoTracking()
            .Where(a => a.PersonaId == personaId && a.ContextoOperativoId == contextoId)
            .OrderByDescending(a => a.FechaHoraInicio)
            .Select(a => new AsignacionUnidadOrganizativaDto(
                a.Id, a.PersonaId, a.ContextoOperativoId, a.UnidadOrganizativaId,
                a.FechaHoraInicio, a.FechaHoraFin, a.Estado, a.MotivoFin, a.RevocadoPorPertenenciaId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifica que la unidad pertenezca al árbol de la Compañía Principal del contexto (RF-055).
    /// </summary>
    private async Task ValidarUnidadDeLaPrincipalAsync(
        Guid unidadOrganizativaId,
        ContextoOperativoPersonaPrincipal contexto,
        CancellationToken ct)
    {
        var unidad = await db.UnidadesOrganizativas
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unidadOrganizativaId, ct)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado, "Unidad organizativa no encontrada.");

        // La unidad no guarda su compañía (RF-044): se resuelve subiendo hasta la raíz del árbol.
        var raizId = unidad.UnidadSuperiorId is null
            ? unidad.Id
            : await jerarquia
                .ObtenerRaizAsync(
                    Jerarquias.TablaUnidadOrganizativa,
                    Jerarquias.ColumnaPadreUnidadOrganizativa,
                    unidad.Id,
                    ct)
                .ConfigureAwait(false);

        var principalDeLaUnidad = await db.RaicesUnidadOrganizativa
            .AsNoTracking()
            .Where(e => e.UnidadOrganizativaRaizId == raizId)
            .Select(e => (Guid?)e.CompaniaId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (principalDeLaUnidad != contexto.CompaniaPrincipalId)
        {
            throw new ConflictoEstadoException(
                CodigosError.CompaniaDebeSerPrincipal,
                "La unidad organizativa pertenece al árbol de otra Compañía Principal distinta a la del contexto.");
        }
    }

    internal static AsignacionUnidadOrganizativaDto AMapa(AsignacionPersonaUnidadOrganizativa a) =>
        new(a.Id, a.PersonaId, a.ContextoOperativoId, a.UnidadOrganizativaId,
            a.FechaHoraInicio, a.FechaHoraFin, a.Estado, a.MotivoFin, a.RevocadoPorPertenenciaId);
}
