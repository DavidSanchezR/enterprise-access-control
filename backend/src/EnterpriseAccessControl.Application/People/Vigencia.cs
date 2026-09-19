using EnterpriseAccessControl.Application.Common.Errores;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Normalización de las ventanas de vigencia de las asociaciones temporales (RF-016, RF-039).
/// </summary>
/// <remarks>
/// Las vigencias de este dominio se declaran en días completos, no en instantes arbitrarios: una
/// persona pertenece a una compañía "desde el 1 de agosto hasta el 31 de julio", no "desde las 14:37".
/// Normalizar el inicio al comienzo del día y el fin al último instante del día evita dos problemas
/// reales: que una vigencia registrada por la tarde deje sin cubrir la mañana de su primer día, y que
/// dos rangos consecutivos declarados por días distintos parezcan solaparse al trigger por unos
/// segundos de diferencia.
/// </remarks>
public static class Vigencia
{
    /// <summary>Inicio del día indicado, en UTC (00:00:00.000).</summary>
    public static DateTime NormalizarInicio(DateTime fechaHora) =>
        new(fechaHora.Year, fechaHora.Month, fechaHora.Day, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Último instante representable del día indicado, en UTC (23:59:59.999).
    /// </summary>
    /// <remarks>
    /// Se usa 23:59:59.999 y no las 00:00 del día siguiente porque la columna es
    /// <c>datetime2(3)</c>: milisegundos es la precisión máxima que persiste, y el valor así
    /// normalizado sigue siendo estrictamente menor que el inicio del día siguiente, de modo que dos
    /// vigencias consecutivas nunca se cruzan.
    /// </remarks>
    public static DateTime NormalizarFin(DateTime fechaHora) =>
        new DateTime(fechaHora.Year, fechaHora.Month, fechaHora.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(1)
            .AddMilliseconds(-1);

    /// <summary>
    /// Normaliza un rango y verifica que el fin sea posterior al inicio (RF-039).
    /// </summary>
    public static (DateTime Inicio, DateTime Fin) NormalizarRango(DateTime inicio, DateTime fin)
    {
        var desde = NormalizarInicio(inicio);
        var hasta = NormalizarFin(fin);

        if (hasta <= desde)
        {
            throw new ConflictoEstadoException(
                CodigosError.PeriodoInvalido,
                "La fecha de fin de vigencia debe ser posterior a la de inicio.");
        }

        return (desde, hasta);
    }
}
