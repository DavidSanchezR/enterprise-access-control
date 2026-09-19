using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Contextos operativos de una persona con cada Compañía Principal (RF-052 a RF-054, RF-072).
/// </summary>
/// <remarks>
/// Responde "¿para qué Principal trabaja esta persona?", que es distinto de "¿quién la contrata?".
/// Una persona puede tener varios contextos vigentes a la vez, uno por Principal, sin límite superior
/// (RF-052, CS-030).
///
/// Qué Principales son admisibles depende del tipo de la compañía de pertenencia:
/// <list type="bullet">
///   <item><b>Caso A</b> — la persona pertenece a una PRINCIPAL_MANDANTE: su único contexto posible
///   es con esa misma Principal (RF-053).</item>
///   <item><b>Caso B</b> — la persona pertenece a una CONTRATISTA: los contextos posibles son las
///   Principales con las que esa contratista tenga relación vigente (RF-054).</item>
/// </list>
/// </remarks>
public sealed class ContextoOperativoService(
    IAppDbContext db,
    ContencionTemporalValidator contencion,
    PersonaService personas,
    IAlcanceCompaniaAccessor alcance,
    IRelojSistema reloj)
{
    public async Task<IReadOnlyList<ContextoOperativoDto>> ListarAsync(
        Guid personaId,
        CancellationToken ct = default)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        return await db.ContextosOperativos
            .AsNoTracking()
            .Where(c => c.PersonaId == personaId)
            .OrderByDescending(c => c.FechaHoraInicio)
            .Select(c => new ContextoOperativoDto(
                c.Id, c.PersonaId, c.CompaniaPrincipalId, c.FechaHoraInicio, c.FechaHoraFin,
                c.Estado, c.MotivoFin, c.RevocadoPorPertenenciaId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<ContextoOperativoDto> AbrirAsync(
        Guid personaId,
        ContextoOperativoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Regla de escritura, no de búsqueda: permite abrir contexto a quien acaba de recibir su
        // pertenencia, sin relajar el alcance.
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var (inicio, fin) = Vigencia.NormalizarRango(request.FechaHoraInicio, request.FechaHoraFin);

        // La pertenencia vigente es el ancla del contexto: sin ella no hay nada que lo justifique
        // (research.md §13) y no habría ventana dentro de la cual contenerlo (RF-072).
        var pertenencia = await contencion
            .ValidarAsync(personaId, inicio, fin, ct)
            .ConfigureAwait(false);

        await ValidarPrincipalAdmisibleAsync(pertenencia, request.CompaniaPrincipalId, ct)
            .ConfigureAwait(false);

        var contexto = new ContextoOperativoPersonaPrincipal
        {
            PersonaId = personaId,
            CompaniaPrincipalId = request.CompaniaPrincipalId,
            FechaHoraInicio = inicio,
            FechaHoraFin = fin,
            Estado = Estado.ACTIVO,
        };

        await db.EjecutarEnTransaccionAsync(
            async cancelacion =>
            {
                // contracts/people.yaml: abrir un contexto "cierra automáticamente cualquier contexto
                // activo previo para la misma Compañía Principal". Sin este cierre, la escritura
                // chocaría con el trigger de no-solapamiento y el usuario vería un error de
                // infraestructura en lugar de la sustitución que pidió.
                var previo = await db.ContextosOperativos
                    .Where(c => c.PersonaId == personaId
                                && c.CompaniaPrincipalId == request.CompaniaPrincipalId
                                && c.Estado == Estado.ACTIVO
                                && c.FechaHoraFin > inicio)
                    .OrderByDescending(c => c.FechaHoraInicio)
                    .FirstOrDefaultAsync(cancelacion)
                    .ConfigureAwait(false);

                if (previo is not null)
                {
                    if (inicio <= previo.FechaHoraInicio)
                    {
                        throw new ConflictoEstadoException(
                            CodigosError.SolapamientoVigencia,
                            "El nuevo contexto debe iniciar después del inicio del contexto vigente con esa Compañía Principal.");
                    }

                    // Cierre por reemplazo: termina justo antes de que empiece el nuevo. No es una
                    // revocación en cascada, así que no se registra pertenencia de origen.
                    CierreDeVigencia.Cerrar(
                        previo,
                        inicio.AddMilliseconds(-1),
                        MotivoFinRevocacion.REEMPLAZO_ASIGNACION,
                        revocadoPorPertenenciaId: null);

                    // El cierre se confirma antes del alta: el trigger se evalúa por sentencia y
                    // vería ambos contextos abiertos si el INSERT se adelantara al UPDATE.
                    await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                }

                db.ContextosOperativos.Add(contexto);
                await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            },
            ct)
            .ConfigureAwait(false);

        return AMapa(contexto);
    }

    /// <summary>
    /// Comprueba que la Compañía Principal sea admisible para la pertenencia de la persona
    /// (RF-053 Caso A / RF-054 Caso B).
    /// </summary>
    private async Task ValidarPrincipalAdmisibleAsync(
        AsignacionPersonaCompania pertenencia,
        Guid companiaPrincipalId,
        CancellationToken ct)
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
                "El contexto operativo debe abrirse con una compañía PRINCIPAL_MANDANTE.");
        }

        var companiaPertenencia = await db.Companias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == pertenencia.CompaniaId, ct)
            .ConfigureAwait(false)
            ?? throw new ConflictoEstadoException(
                CodigosError.RecursoNoEncontrado,
                "La compañía de pertenencia de la persona no existe.");

        if (companiaPertenencia.TipoCompania == TipoCompania.PRINCIPAL_MANDANTE)
        {
            // Caso A: la persona es de la propia Principal; no puede abrir contexto con otra.
            if (companiaPrincipalId != companiaPertenencia.Id)
            {
                throw new ReglaNegocioInvalidaException(
                    CodigosError.PrincipalNoCorrespondeAPertenencia,
                    "La persona pertenece a una Compañía Principal y solo puede operar en esa misma compañía.");
            }

            return;
        }

        // Caso B: la persona es de una Contratista; solo valen las Principales con relación vigente.
        var ahora = reloj.UtcNow;

        var hayRelacionVigente = await db.RelacionesContratistaPrincipal
            .AsNoTracking()
            .AnyAsync(
                r => r.CompaniaContratistaId == companiaPertenencia.Id
                     && r.CompaniaPrincipalId == companiaPrincipalId
                     && r.FechaHoraInicio <= ahora
                     && (r.FechaHoraFin == null || ahora < r.FechaHoraFin),
                ct)
            .ConfigureAwait(false);

        if (!hayRelacionVigente)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.SinRelacionContratistaPrincipalVigente,
                "No existe una relación vigente entre la compañía contratista de la persona y esa Compañía Principal.");
        }
    }

    internal static ContextoOperativoDto AMapa(ContextoOperativoPersonaPrincipal c) =>
        new(c.Id, c.PersonaId, c.CompaniaPrincipalId, c.FechaHoraInicio, c.FechaHoraFin,
            c.Estado, c.MotivoFin, c.RevocadoPorPertenenciaId);
}
