namespace EnterpriseAccessControl.Domain.Enums;

/// <summary>
/// Estado administrativo de una pertenencia Persona–Compañía (RF-063, research.md §14.2).
/// </summary>
/// <remarks>
/// **No es la fuente de verdad para autorización.** La vigencia efectiva se determina siempre
/// comparando <c>FechaHoraInicio</c>/<c>FechaHoraFin</c> contra la fecha evaluada (Principio IV).
/// Este campo existe para que la interfaz y los informes de auditoría puedan explicar *por qué*
/// terminó una pertenencia sin volver a derivarlo.
/// </remarks>
public enum EstadoPertenencia
{
    ACTIVA,
    FINALIZADA,
}

/// <summary>Causa del cierre de una pertenencia (research.md §14.2).</summary>
public enum MotivoFinPertenencia
{
    /// <summary>Cese explícito: la persona deja la compañía sin que otra la reemplace.</summary>
    CESE_PERTENENCIA,

    /// <summary>Una nueva pertenencia sustituyó a esta (cambio de compañía).</summary>
    REEMPLAZO_ASIGNACION,
}

/// <summary>
/// Causa del cierre de una asociación dependiente de la pertenencia (research.md §14.2).
/// </summary>
public enum MotivoFinRevocacion
{
    /// <summary>Revocación automática en cascada al cerrarse la pertenencia que la sustentaba (RF-061).</summary>
    REVOCACION_CESE_PERTENENCIA,

    /// <summary>Otra asociación del mismo ámbito la sustituyó.</summary>
    REEMPLAZO_ASIGNACION,

    /// <summary>Cierre administrativo directo, sin cascada ni reemplazo.</summary>
    CIERRE_MANUAL,
}

/// <summary>
/// Estado de una credencial asignada (Historia 9, RF-061).
/// </summary>
/// <remarks>
/// <see cref="REVOCADA"/> se distingue de <see cref="DEVUELTO"/> (devolución física voluntaria) y de
/// <see cref="ELIMINADO"/> (baja lógica administrativa): el acceso se invalidó por una causa ajena a
/// la credencial misma — el cese de la pertenencia que la sustentaba.
///
/// Los tres estados de cierre son terminales. El mero vencimiento de <c>FechaHoraFin</c> **no**
/// cambia el estado (RF-070): solo una acción administrativa explícita lo hace.
/// </remarks>
public enum EstadoCredencial
{
    ASIGNADO,
    DEVUELTO,
    ELIMINADO,
    REVOCADA,
}
