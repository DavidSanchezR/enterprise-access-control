using System.ComponentModel.DataAnnotations;

namespace EnterpriseAccessControl.Application.Common.Options;

/// <summary>
/// Zona horaria empresarial usada para evaluar bloques horarios (RF-022, research.md §5).
/// Los timestamps se persisten siempre en UTC; esta zona solo se aplica al evaluar reglas de
/// negocio horarias y al presentar fechas (Constitución, Reglas de Arquitectura e Ingeniería).
/// </summary>
public sealed class ZonaHorariaOptions
{
    public const string SectionName = "ZonaHoraria";

    /// <summary>Identificador IANA; Perú es el locale inicial (spec.md, Supuestos).</summary>
    [Required(AllowEmptyStrings = false)]
    public string TimeZoneId { get; init; } = "America/Lima";
}
