namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Conversión entre UTC y la zona horaria de una Compañía Principal (research.md §5, §31; RF-080).
/// </summary>
/// <remarks>
/// Se declara en el Dominio —no en Infraestructura— para que <c>EvaluadorDeAcceso</c> pueda
/// evaluar bloques horarios sin dejar de ser un servicio de dominio puro: el Dominio define el
/// puerto y la Infraestructura aporta la implementación con NodaTime (research.md §15, §18).
///
/// Los timestamps se persisten siempre en UTC; esta conversión existe porque los bloques horarios
/// de un permiso son reglas de negocio expresadas en hora local (p. ej. "lunes de 08:00 a 17:00"),
/// y aplicarlas requiere conocer la hora local del instante evaluado, incluyendo cambios de offset.
///
/// **Desde la Sesión 2026-09-20 (D5) cada Compañía Principal tiene su propia zona** y estos métodos
/// la reciben explícitamente, en lugar de resolverla contra una única zona de proceso: dos
/// Principales en husos distintos deben evaluar el mismo instante UTC contra sus propios bloques.
/// Un <c>null</c> significa "no resoluble a una única Principal" y cae en la zona global de
/// respaldo (<c>ZonaHoraria:TimeZoneId</c>), que es lo que rige para las entidades no ligadas a una
/// Principal concreta.
/// </remarks>
public interface IRelojEmpresarial
{
    /// <summary>Día de la semana, en la zona indicada, del instante UTC dado.</summary>
    DayOfWeek DiaSemanaLocal(DateTime instanteUtc, string? zonaIana);

    /// <summary>Hora local (sin fecha) en la zona indicada del instante UTC dado.</summary>
    TimeOnly HoraLocal(DateTime instanteUtc, string? zonaIana);

    /// <summary>
    /// Zona efectivamente aplicada: la indicada si es utilizable, o la global de respaldo.
    /// </summary>
    /// <remarks>
    /// Se expone para que la respuesta de evaluación pueda informar contra qué zona se resolvieron
    /// los bloques horarios, en vez de dejar al operador adivinarlo.
    /// </remarks>
    string ZonaEfectiva(string? zonaIana);

    /// <summary>Indica si el identificador es una zona IANA reconocida (RF-080).</summary>
    bool EsZonaValida(string? zonaIana);
}
