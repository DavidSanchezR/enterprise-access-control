namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Conversión entre UTC y la zona horaria empresarial (research.md §5, RF-022).
/// </summary>
/// <remarks>
/// Se declara en el Dominio —no en Infraestructura— para que <c>EvaluadorDeAcceso</c> pueda
/// evaluar bloques horarios sin dejar de ser un servicio de dominio puro: el Dominio define el
/// puerto y la Infraestructura aporta la implementación con NodaTime (research.md §15, §18).
///
/// Los timestamps se persisten siempre en UTC; esta conversión existe porque los bloques horarios
/// de un permiso son reglas de negocio expresadas en hora local (p. ej. "lunes de 08:00 a 17:00"),
/// y aplicarlas requiere conocer la hora local del instante evaluado, incluyendo cambios de offset.
/// </remarks>
public interface IRelojEmpresarial
{
    /// <summary>Día de la semana, en la zona horaria empresarial, del instante UTC indicado.</summary>
    DayOfWeek DiaSemanaLocal(DateTime instanteUtc);

    /// <summary>Hora local (sin fecha) en la zona horaria empresarial del instante UTC indicado.</summary>
    TimeOnly HoraLocal(DateTime instanteUtc);
}
