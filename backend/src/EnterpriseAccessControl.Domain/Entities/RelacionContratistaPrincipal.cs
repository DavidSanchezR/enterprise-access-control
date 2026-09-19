using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Vínculo temporal entre una Compañía CONTRATISTA y una Compañía PRINCIPAL_MANDANTE (RF-051).
/// </summary>
/// <remarks>
/// Una misma Contratista puede mantener relaciones vigentes simultáneas con varias Principales
/// (CS-012); lo que no puede haber es más de una relación vigente para el mismo par
/// <c>(Contratista, Principal)</c>. Esa exclusividad la impone un trigger
/// <c>AFTER INSERT, UPDATE</c> particionado por ese par (research.md §5), no sólo la aplicación:
/// dos peticiones concurrentes podrían pasar ambas la validación previa.
///
/// **<see cref="FechaHoraFin"/> es opcional en esta entidad.** RF-071 obliga a informar la fecha de
/// fin en las asociaciones temporales vinculadas a una <c>Persona</c>, y ésta vincula dos compañías;
/// aquí <c>null</c> conserva su significado de vigencia abierta.
/// </remarks>
public class RelacionContratistaPrincipal : EntidadBase
{
    public required Guid CompaniaContratistaId { get; set; }

    public required Guid CompaniaPrincipalId { get; set; }

    public required DateTime FechaHoraInicio { get; set; }

    /// <summary><c>null</c> significa vigencia abierta (ver nota sobre RF-071 en la clase).</summary>
    public DateTime? FechaHoraFin { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Indica si la relación está vigente en el instante indicado.</summary>
    /// <remarks>
    /// La vigencia se evalúa dinámicamente contra las fechas y nunca se deriva de un campo de estado
    /// almacenado: el mero paso del tiempo no debe modificar filas (Principio IV).
    /// </remarks>
    public bool EstaVigenteEn(DateTime instante) =>
        FechaHoraInicio <= instante && (FechaHoraFin is null || instante < FechaHoraFin);
}
