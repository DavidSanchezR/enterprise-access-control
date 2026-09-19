using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Histórico de pertenencia Persona–Compañía: alta, cese y renovación
/// (RF-014, RF-016, RF-061, RF-073).
/// </summary>
/// <remarks>
/// Las tres operaciones normalizan las horas a inicio/fin de día (RF-016) y se ejecutan dentro de una
/// transacción, porque el cierre de una pertenencia y la revocación de sus dependientes tienen que
/// ser atómicos.
///
/// **Alta y cese disparan la cascada; la renovación no.** Renovar extiende el techo temporal sin
/// cerrar nada, así que no hay dependiente que revocar: es la contraparte inversa de finalizar.
/// </remarks>
public sealed class HistorialPersonaService(
    IAppDbContext db,
    RevocacionService revocacion,
    PersonaService personas,
    IAlcanceCompaniaAccessor alcance,
    IRelojSistema reloj)
{
    public async Task<IReadOnlyList<AsignacionCompaniaDto>> ListarAsync(
        Guid personaId,
        CancellationToken ct = default)
    {
        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        return await db.AsignacionesPersonaCompania
            .AsNoTracking()
            .Where(a => a.PersonaId == personaId)
            .OrderByDescending(a => a.FechaHoraInicio)
            .Select(a => new AsignacionCompaniaDto(
                a.Id, a.PersonaId, a.CompaniaId, a.FechaHoraInicio, a.FechaHoraFin,
                a.Estado, a.MotivoFin))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Registra una nueva pertenencia, cerrando la anterior y revocando sus dependientes
    /// (RF-014, RF-061).
    /// </summary>
    public async Task<AsignacionCompaniaDto> CrearAsignacionAsync(
        Guid personaId,
        AsignacionCompaniaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        if (!alcance.EstaEnAlcance(request.CompaniaId))
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado, "Compañía no encontrada.");
        }

        var (inicio, fin) = Vigencia.NormalizarRango(request.FechaHoraInicio, request.FechaHoraFin);

        var nueva = new AsignacionPersonaCompania
        {
            PersonaId = personaId,
            CompaniaId = request.CompaniaId,
            FechaHoraInicio = inicio,
            FechaHoraFin = fin,
            Estado = EstadoPertenencia.ACTIVA,
        };

        await db.EjecutarEnTransaccionAsync(
            async cancelacion =>
            {
                var previa = await ObtenerActivaAsync(personaId, cancelacion).ConfigureAwait(false);

                if (previa is not null)
                {
                    // El cierre no puede extender una pertenencia que ya terminaba antes:
                    // CerrarPertenencia toma el menor entre su fin declarado y el inicio de la nueva.
                    CerrarPertenencia(previa, inicio, MotivoFinPertenencia.REEMPLAZO_ASIGNACION);

                    await revocacion
                        .RevocarDependientesAsync(
                            previa, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, cancelacion)
                        .ConfigureAwait(false);

                    // Se confirma el cierre antes de insertar: el trigger de no-solapamiento se
                    // evalúa por sentencia y vería ambas pertenencias abiertas si el INSERT se
                    // adelantara al UPDATE.
                    await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                }

                db.AsignacionesPersonaCompania.Add(nueva);
                await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            },
            ct)
            .ConfigureAwait(false);

        return AMapa(nueva);
    }

    /// <summary>Cese explícito: cierra la pertenencia sin que otra la reemplace (RF-061).</summary>
    public async Task FinalizarAsync(
        Guid personaId,
        Guid asignacionId,
        FinalizarPertenenciaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var pertenencia = await ObtenerParaEscrituraAsync(personaId, asignacionId, ct)
            .ConfigureAwait(false);

        if (pertenencia.Estado != EstadoPertenencia.ACTIVA)
        {
            throw new ConflictoEstadoException(
                "PERTENENCIA_NO_VIGENTE", "La pertenencia ya fue finalizada.");
        }

        var cierre = Vigencia.NormalizarFin(request.FechaHoraFin);

        if (cierre < pertenencia.FechaHoraInicio)
        {
            throw new ConflictoEstadoException(
                CodigosError.PeriodoInvalido,
                "La fecha de cese no puede ser anterior al inicio de la pertenencia.");
        }

        await db.EjecutarEnTransaccionAsync(
            async cancelacion =>
            {
                CerrarPertenencia(pertenencia, cierre, MotivoFinPertenencia.CESE_PERTENENCIA);

                // La cascada se ejecuta de inmediato aunque la fecha de cese sea futura: propaga esa
                // misma fecha, y los dependientes siguen genuinamente vigentes hasta que llegue
                // (RF-064, research.md §14.3). No se agenda ningún proceso futuro.
                await revocacion
                    .RevocarDependientesAsync(
                        pertenencia, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, cancelacion)
                    .ConfigureAwait(false);

                await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            },
            ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Extiende la vigencia de la pertenencia sin cerrarla ni tocar sus dependientes (RF-073).
    /// </summary>
    public async Task RenovarAsync(
        Guid personaId,
        Guid asignacionId,
        RenovarPertenenciaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await personas.ExigirAlcanceHistoricoAsync(personaId, ct).ConfigureAwait(false);

        var pertenencia = await ObtenerParaEscrituraAsync(personaId, asignacionId, ct)
            .ConfigureAwait(false);

        // Renovable exige las dos condiciones: estado ACTIVA y seguir vigente por fechas. Una
        // pertenencia ACTIVA pero ya expirada no se renueva —eso puentearía retroactivamente un vacío
        // temporal ya transcurrido—; requiere una pertenencia nueva.
        if (!pertenencia.EsRenovableEn(reloj.UtcNow))
        {
            throw new ConflictoEstadoException(
                CodigosError.PertenenciaNoRenovable,
                "La pertenencia no es renovable: está finalizada o su vigencia ya expiró.");
        }

        var nuevoFin = Vigencia.NormalizarFin(request.FechaHoraFin);

        if (nuevoFin <= pertenencia.FechaHoraFin)
        {
            throw new ConflictoEstadoException(
                CodigosError.RenovacionNoPosterior,
                "La nueva fecha de fin debe ser estrictamente posterior a la vigente.");
        }

        // Solo se mueve el techo temporal. Estado, motivo y fecha de inicio quedan intactos, y no se
        // dispara ninguna cascada: no hay nada que cerrar.
        pertenencia.FechaHoraFin = nuevoFin;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // --- Apoyo ---------------------------------------------------------------------------------

    private static void CerrarPertenencia(
        AsignacionPersonaCompania pertenencia,
        DateTime cierre,
        MotivoFinPertenencia motivo)
    {
        // Un cierre solo acorta: si ya terminaba antes, se respeta la fecha original.
        pertenencia.FechaHoraFin = CierreDeVigencia.Acortar(pertenencia.FechaHoraFin, cierre);

        pertenencia.Estado = EstadoPertenencia.FINALIZADA;
        pertenencia.MotivoFin = motivo;
    }

    private Task<AsignacionPersonaCompania?> ObtenerActivaAsync(
        Guid personaId,
        CancellationToken ct) =>
        db.AsignacionesPersonaCompania
            .Where(a => a.PersonaId == personaId && a.Estado == EstadoPertenencia.ACTIVA)
            .OrderByDescending(a => a.FechaHoraInicio)
            .FirstOrDefaultAsync(ct);

    private async Task<AsignacionPersonaCompania> ObtenerParaEscrituraAsync(
        Guid personaId,
        Guid asignacionId,
        CancellationToken ct) =>
        await db.AsignacionesPersonaCompania
            .FirstOrDefaultAsync(a => a.Id == asignacionId && a.PersonaId == personaId, ct)
            .ConfigureAwait(false)
        ?? throw new RecursoNoEncontradoException(
            CodigosError.RecursoNoEncontrado, "Asignación de compañía no encontrada.");


    private static AsignacionCompaniaDto AMapa(AsignacionPersonaCompania a) =>
        new(a.Id, a.PersonaId, a.CompaniaId, a.FechaHoraInicio, a.FechaHoraFin, a.Estado, a.MotivoFin);
}
