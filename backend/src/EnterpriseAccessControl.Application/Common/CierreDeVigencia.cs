using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Common;

/// <summary>
/// Reglas comunes de cierre de una vigencia ya declarada (RF-063, RF-064; research.md §14, §24).
/// </summary>
/// <remarks>
/// Antes vivían copiadas en cada caso de uso que cierra algo: la revocación en cascada, el reemplazo
/// de contexto y de unidad organizativa, el cese y reemplazo de pertenencia, y la devolución o baja de
/// credencial. Concentrarlas aquí hace que la regla más delicada —un cierre solo acorta, nunca extiende—
/// tenga una única implementación y una única batería de pruebas.
///
/// Solo se agrupa lo que era idéntico. Qué instante de cierre corresponde a cada operación (el cese
/// declarado, el inicio de la asociación que reemplaza, el momento de la devolución) sigue siendo
/// decisión de cada caso de uso: esas fechas difieren entre operaciones y forman parte del
/// comportamiento validado.
/// </remarks>
public static class CierreDeVigencia
{
    /// <summary>
    /// Fecha de fin tras un cierre: la menor entre la ya declarada y la del cierre.
    /// </summary>
    /// <remarks>
    /// Una vigencia que ya terminaba antes conserva su fecha original: extenderla hasta la del cierre
    /// alargaría una vigencia que nadie concedió. Solo la renovación explícita (RF-073) extiende.
    /// </remarks>
    public static DateTime Acortar(DateTime finDeclarado, DateTime cierre) =>
        finDeclarado < cierre ? finDeclarado : cierre;

    /// <summary>
    /// Cierra un contexto operativo o una asignación de unidad organizativa.
    /// </summary>
    /// <param name="asociacion">Asociación a cerrar.</param>
    /// <param name="cierre">Instante efectivo del cierre; nunca extiende el fin declarado.</param>
    /// <param name="motivo">Causa del cierre (RF-063).</param>
    /// <param name="revocadoPorPertenenciaId">
    /// Pertenencia que originó la cascada, o <c>null</c> si el cierre no vino de ella: una referencia en
    /// un cierre manual o por reemplazo apuntaría a una pertenencia que no lo provocó (research.md §14.2).
    /// </param>
    public static void Cerrar(
        AsociacionRevocable asociacion,
        DateTime cierre,
        MotivoFinRevocacion motivo,
        Guid? revocadoPorPertenenciaId)
    {
        ArgumentNullException.ThrowIfNull(asociacion);

        asociacion.FechaHoraFin = Acortar(asociacion.FechaHoraFin, cierre);
        asociacion.Estado = Estado.INACTIVO;
        asociacion.MotivoFin = motivo;
        asociacion.RevocadoPorPertenenciaId = revocadoPorPertenenciaId;

        // FechaHoraInicio nunca se toca: el histórico debe seguir diciendo cuándo empezó (RF-063).
    }

    /// <summary>
    /// Cierra una credencial asignada llevándola a un estado terminal.
    /// </summary>
    /// <remarks>
    /// Cada estado terminal tiene una única causa (research.md §23): <c>DEVUELTO</c> la devolución
    /// física, <c>ELIMINADO</c> la baja administrativa y <c>REVOCADA</c> la cascada, que es la única que
    /// registra la pertenencia de origen.
    /// </remarks>
    public static void Cerrar(
        AsignacionCredencial credencial,
        DateTime cierre,
        EstadoCredencial estadoFinal,
        Guid? revocadoPorPertenenciaId)
    {
        ArgumentNullException.ThrowIfNull(credencial);

        if (estadoFinal == EstadoCredencial.ASIGNADO)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estadoFinal), "Cerrar una credencial exige un estado terminal.");
        }

        credencial.FechaHoraFin = Acortar(credencial.FechaHoraFin, cierre);
        credencial.Estado = estadoFinal;
        credencial.RevocadoPorPertenenciaId = revocadoPorPertenenciaId;
    }
}
