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
/// Se aplica solo a las tres asociaciones que RF-061 declara dependientes —contexto operativo, unidad
/// organizativa y credencial—. <c>AsignaciónTipoPersona</c> queda fuera a propósito: el modelo de
/// dominio no la declara dependiente de la pertenencia (RF-011), y aplicarle la contención sería
/// inventar una dependencia que nadie estableció.
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
