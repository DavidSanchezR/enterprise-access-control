using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Contención temporal jerárquica de las asociaciones dependientes de la pertenencia (RF-072).
/// </summary>
/// <remarks>
/// Una asociación que depende de la pertenencia Persona–Compañía no puede sobrevivirla ni precederla:
/// su ventana debe caber íntegra dentro de la de la pertenencia. Sin esta regla, un contexto operativo
/// podría seguir "vigente" después de que la persona dejara de pertenecer a la compañía que lo
/// justificaba.
///
/// La comprobación usa desigualdades **no estrictas** en ambos extremos: que la asociación empiece y
/// termine exactamente con la pertenencia es el caso normal, no un error (CS-034).
///
/// Se aplica a las tres asociaciones que RF-061 declara dependientes —contexto operativo, unidad
/// organizativa y credencial— y, desde el cambio post-Baseline VF-007 (RF-082), también al perfil
/// (<c>AsignaciónTipoPersona</c>) y a los permisos de alcance PERSONA. Estos dos últimos se contienen
/// pero no pasan a depender de la pertenencia: la cascada de RF-061 sigue sin alcanzarlos.
///
/// Desde el cambio post-Baseline VF-004 (RF-083), los permisos se contienen por <b>fecha civil</b> con
/// <see cref="ValidarFechasCivilesAsync"/>: su vigencia es de días completos en la zona de la Principal del
/// área, y compararla por instantes con la de la pertenencia rechazaría el mismo último día solo por la
/// diferencia de representación UTC (research.md §36.4). <see cref="ValidarAsync"/> sigue siendo la
/// comprobación de RF-072 y de los perfiles, sin cambios.
/// </remarks>
public sealed class ContencionTemporalValidator(IAppDbContext db, IRelojSistema reloj)
{
    /// <summary>
    /// Verifica que el rango propuesto quepa dentro de la pertenencia vigente de la persona.
    /// </summary>
    /// <returns>La pertenencia que contiene el rango, para que el llamador la reutilice como ancla.</returns>
    public async Task<AsignacionPersonaCompania> ValidarAsync(
        Guid personaId,
        DateTime inicio,
        DateTime fin,
        CancellationToken ct = default)
    {
        var pertenencia = await ObtenerPertenenciaVigenteAsync(personaId, ct).ConfigureAwait(false);

        Validar(pertenencia, inicio, fin);

        return pertenencia;
    }

    /// <summary>
    /// Comprobación pura, sin acceso a datos: aisla la regla para poder ejercitarla directamente.
    /// </summary>
    public static void Validar(AsignacionPersonaCompania pertenencia, DateTime inicio, DateTime fin)
    {
        ArgumentNullException.ThrowIfNull(pertenencia);

        if (inicio < pertenencia.FechaHoraInicio)
        {
            throw new ConflictoEstadoException(
                CodigosError.FueraDeContencionTemporal,
                "La vigencia no puede comenzar antes que la pertenencia de la persona a su compañía.");
        }

        if (fin > pertenencia.FechaHoraFin)
        {
            throw new ConflictoEstadoException(
                CodigosError.FueraDeContencionTemporal,
                "La vigencia no puede extenderse más allá de la pertenencia de la persona a su compañía.");
        }
    }

    /// <summary>
    /// Verifica que las fechas civiles de un permiso queden dentro de las de la pertenencia vigente de la
    /// persona (RF-082 para <c>PermisoAcceso</c>, RF-083 (c)).
    /// </summary>
    public async Task<AsignacionPersonaCompania> ValidarFechasCivilesAsync(
        Guid personaId,
        DateOnly inicio,
        DateOnly fin,
        CancellationToken ct = default)
    {
        var pertenencia = await ObtenerPertenenciaVigenteAsync(personaId, ct).ConfigureAwait(false);

        ValidarFechasCiviles(pertenencia, inicio, fin);

        return pertenencia;
    }

    /// <summary>
    /// Comprobación pura por fecha civil, con igualdad válida en ambos extremos (D1).
    /// </summary>
    /// <remarks>
    /// La fecha de la pertenencia es la que ella <b>declara</b>: toda <c>AsignaciónPersonaCompañía</c> se
    /// normaliza con <c>Vigencia</c>, que guarda el día declarado en sus límites UTC, así que sus componentes
    /// de fecha UTC son exactamente ese día. Nunca se convierten a la zona del permiso: en Lima, las 00:00 UTC
    /// del primer día caen en el día anterior y se rechazaría el primer día.
    /// </remarks>
    public static void ValidarFechasCiviles(AsignacionPersonaCompania pertenencia, DateOnly inicio, DateOnly fin)
    {
        ArgumentNullException.ThrowIfNull(pertenencia);

        if (inicio < DateOnly.FromDateTime(pertenencia.FechaHoraInicio))
        {
            throw new ConflictoEstadoException(
                CodigosError.FueraDeContencionTemporal,
                "La vigencia no puede comenzar antes que la pertenencia de la persona a su compañía.");
        }

        if (fin > DateOnly.FromDateTime(pertenencia.FechaHoraFin))
        {
            throw new ConflictoEstadoException(
                CodigosError.FueraDeContencionTemporal,
                "La vigencia no puede extenderse más allá de la pertenencia de la persona a su compañía.");
        }
    }

    /// <summary>
    /// Pertenencia vigente de la persona, que sirve de ancla a toda asociación dependiente
    /// (research.md §13).
    /// </summary>
    /// <remarks>
    /// Sin pertenencia vigente no hay nada que contener ni ancla que justifique la asociación: se
    /// rechaza en lugar de crear un registro huérfano.
    /// </remarks>
    public async Task<AsignacionPersonaCompania> ObtenerPertenenciaVigenteAsync(
        Guid personaId,
        CancellationToken ct = default)
    {
        var ahora = reloj.UtcNow;

        // Vigencia evaluada por fechas, nunca por Estado en aislamiento (Principio IV, RF-063).
        var pertenencia = await db.AsignacionesPersonaCompania
            .AsNoTracking()
            .Where(a => a.PersonaId == personaId
                        && a.FechaHoraInicio <= ahora
                        && ahora < a.FechaHoraFin)
            .OrderByDescending(a => a.FechaHoraInicio)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // 400 y no 409: contracts/people.yaml documenta expresamente este caso entre los de
        // "solicitud inválida". No es un conflicto con el estado de otro recurso, sino una
        // precondición de la propia petición que no se cumple.
        return pertenencia ?? throw new ReglaNegocioInvalidaException(
            CodigosError.SinPertenenciaVigente,
            "La persona no tiene una compañía de pertenencia vigente.");
    }
}
