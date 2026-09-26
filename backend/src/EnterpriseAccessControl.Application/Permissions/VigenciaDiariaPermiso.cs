using System.Globalization;
using EnterpriseAccessControl.Application.Common.Errores;
using NodaTime;

namespace EnterpriseAccessControl.Application.Permissions;

/// <summary>
/// Vigencia diaria de un <c>PermisoAcceso</c>: conversión entre fechas civiles y los instantes UTC que se
/// persisten y evalúan (RF-083; research.md §36.1, §36.3).
/// </summary>
/// <remarks>
/// Cambio post-Baseline VF-004. Cada fecha representa un día civil completo en la zona de la Compañía
/// Principal propietaria del área. El inicio es el <b>primer instante válido</b> del día y el fin, el primer
/// instante válido del día siguiente menos 1 ms (F-1). Ambos se obtienen con
/// <see cref="DateTimeZone.AtStartOfDay(LocalDate)"/>, que resuelve los cambios de horario con tzdb: si las
/// 00:00 no existen porque el reloj salta a la 01:00, el día empieza a la 01:00; si se repiten, se toma la
/// primera. Por eso el día dura 23, 24 o 25 horas sin aritmética manual sobre <see cref="DateTime"/>, y por eso
/// no se usa <c>Vigencia.NormalizarInicio/NormalizarFin</c>, que normalizan el día UTC.
///
/// Es una clase pura: recibe la zona ya resuelta y no accede a datos ni al reloj. La evaluación de acceso no
/// la usa: sigue comparando los instantes persistidos (<c>PermisoAcceso.EstaVigenteEn</c>).
/// </remarks>
public static class VigenciaDiariaPermiso
{
    /// <summary>Precisión de la columna <c>datetime2(3)</c>: el fin es el instante anterior al día siguiente.</summary>
    private static readonly Duration UnMilisegundo = Duration.FromMilliseconds(1);

    /// <summary>Zona tzdb de un identificador ya resuelto con <c>IRelojEmpresarial.ZonaEfectiva</c>.</summary>
    public static DateTimeZone Zona(string zonaEfectiva) => DateTimeZoneProviders.Tzdb[zonaEfectiva];

    /// <summary>Primer instante válido, en UTC, de la fecha civil en la zona.</summary>
    public static DateTime InicioUtc(DateOnly fecha, DateTimeZone zona) =>
        InicioDelDia(LocalDate.FromDateOnly(fecha), zona).ToDateTimeUtc();

    /// <summary>Último instante representable, en UTC, de la fecha civil en la zona.</summary>
    public static DateTime FinUtc(DateOnly fecha, DateTimeZone zona) =>
        (InicioDelDia(LocalDate.FromDateOnly(fecha).PlusDays(1), zona) - UnMilisegundo).ToDateTimeUtc();

    /// <summary>Fecha civil, en la zona, de un instante UTC.</summary>
    public static DateOnly FechaCivil(DateTime instanteUtc, DateTimeZone zona) =>
        Instant.FromDateTimeUtc(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc))
            .InZone(zona)
            .Date
            .ToDateOnly();

    /// <summary>
    /// Indica si la vigencia coincide exactamente con los límites de días civiles completos en la zona
    /// (RF-083 (f)).
    /// </summary>
    /// <remarks>
    /// Es <c>false</c> en los permisos anteriores a VF-004 que se guardaron con hora, y también en uno
    /// normalizado en otra zona si después cambia la zona de su Principal (RF-083 (h)).
    /// </remarks>
    public static bool EsDiaCompleto(DateTime inicioUtc, DateTime finUtc, DateTimeZone zona) =>
        inicioUtc == InicioUtc(FechaCivil(inicioUtc, zona), zona)
        && finUtc == FinUtc(FechaCivil(finUtc, zona), zona);

    /// <summary>
    /// Resuelve un extremo de una actualización (F-6): conserva el instante almacenado si su fecha civil no
    /// cambia y, si cambia, lo normaliza a la fecha solicitada.
    /// </summary>
    /// <remarks>
    /// Así, reenviar las fechas de un permiso anterior a VF-004 al cambiar solo sus bloques o su estado no
    /// altera su vigencia, y editar un extremo nunca reescribe el otro (RF-083 (d), (g)).
    /// </remarks>
    public static (DateTime Instante, bool Cambia) ResolverExtremo(
        DateTime almacenadoUtc,
        DateOnly solicitada,
        DateTimeZone zona,
        bool esFin)
    {
        if (FechaCivil(almacenadoUtc, zona) == solicitada)
        {
            return (almacenadoUtc, false);
        }

        return (esFin ? FinUtc(solicitada, zona) : InicioUtc(solicitada, zona), true);
    }

    private static Instant InicioDelDia(LocalDate fecha, DateTimeZone zona)
    {
        try
        {
            return zona.AtStartOfDay(fecha).ToInstant();
        }
        catch (SkippedTimeException)
        {
            // Solo ocurre si la zona omite el día entero, algo que tzdb registra únicamente en fechas
            // históricas (p. ej. Pacific/Apia, 30/12/2011). La fecha no existe: es una entrada inválida.
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                $"La fecha {fecha.ToString("uuuu-MM-dd", CultureInfo.InvariantCulture)} no existe en la zona horaria de la Compañía Principal del área.");
        }
    }
}
