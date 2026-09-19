using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Reglas de cierre que aplica la revocación en cascada a cada fila afectada (research.md §14.1).
/// </summary>
/// <remarks>
/// Se separan de <see cref="RevocacionService"/> —que es quien consulta y decide *qué* filas
/// alcanzar— para que el *cómo* se cierra cada fila sea una función pura, verificable sin base de
/// datos. Son tres decisiones que conviene poder auditar por separado: no extender una fecha ya
/// fijada, no tocar nunca la fecha de inicio, y registrar el origen solo cuando la causa fue
/// realmente la cascada.
///
/// El cierre en sí lo implementa <see cref="CierreDeVigencia"/>, compartido con los demás casos de uso
/// que cierran vigencias. Lo que es propio de la cascada —cuándo se registra la pertenencia de origen y
/// que una credencial pase a <c>REVOCADA</c>— se decide aquí.
/// </remarks>
public static class ReglasRevocacion
{
    /// <summary>
    /// Fecha de cierre efectiva: la menor entre la ya declarada y la del cese.
    /// </summary>
    /// <remarks>
    /// Una asociación que ya terminaba antes del cese conserva su fecha original. Extenderla hasta
    /// la del cese alargaría una vigencia que nadie concedió.
    /// </remarks>
    public static DateTime CalcularFechaFin(DateTime actual, DateTime efectivaDelCese) =>
        CierreDeVigencia.Acortar(actual, efectivaDelCese);

    /// <summary>Cierra un contexto operativo o una asignación de unidad organizativa.</summary>
    public static void Cerrar(
        AsociacionRevocable asociacion,
        DateTime efectivaDelCese,
        MotivoFinRevocacion motivo,
        Guid pertenenciaId) =>
        // Solo se registra el origen cuando la causa fue la cascada: en un cierre manual o por
        // reemplazo, la columna apuntaría a una pertenencia que no lo provocó y falsearía la
        // auditoría.
        CierreDeVigencia.Cerrar(
            asociacion,
            efectivaDelCese,
            motivo,
            motivo == MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA ? pertenenciaId : null);

    /// <summary>
    /// Cierra una credencial asignada.
    /// </summary>
    /// <remarks>
    /// No recibe motivo: el valor <c>REVOCADA</c> de su propio estado ya expresa la causa, y añadir
    /// un campo paralelo duplicaría la información (research.md §14.2).
    /// </remarks>
    public static void Revocar(
        AsignacionCredencial credencial,
        DateTime efectivaDelCese,
        Guid pertenenciaId) =>
        CierreDeVigencia.Cerrar(credencial, efectivaDelCese, EstadoCredencial.REVOCADA, pertenenciaId);
}
