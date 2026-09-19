using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Nodo del árbol de áreas físicas de acceso de una Compañía Principal (RF-009, RF-038, RF-046).
/// </summary>
/// <remarks>
/// **Contraste deliberado con <see cref="UnidadOrganizativa"/>**: aquella no puede referenciar a
/// <c>Compañía</c> (RF-044) y resuelve su pertenencia subiendo hasta la raíz; ésta **sí lleva una FK
/// directa** <see cref="CompaniaPrincipalId"/>, porque RF-046 no establece ninguna restricción contra
/// esa relación. Son dos jerarquías que se parecen en forma pero difieren en esto, y la diferencia es
/// intencional (data-model.md, research.md §4).
///
/// La consecuencia práctica es que un área hija **duplica** el identificador de su Principal en lugar
/// de heredarlo por recorrido. Para que esa duplicación no pueda divergir, el valor de un área hija
/// se fija siempre igual al de su padre y no es editable por separado: ni al crearla ni al moverla.
///
/// La ausencia de ciclos (Principio V, RF-038) se valida en el servidor con un CTE recursivo antes de
/// confirmar cualquier creación o reubicación.
/// </remarks>
public class AreaAcceso : EntidadBase
{
    public required string Nombre { get; set; }

    /// <summary>Padre en la jerarquía; <c>null</c> identifica un área raíz.</summary>
    public Guid? AreaSuperiorId { get; set; }

    /// <summary>
    /// Compañía PRINCIPAL_MANDANTE propietaria del área (RF-046).
    /// </summary>
    /// <remarks>
    /// En un área hija coincide siempre con el de su padre. Una CONTRATISTA no posee áreas de acceso.
    /// </remarks>
    public required Guid CompaniaPrincipalId { get; set; }

    /// <summary>
    /// Estado administrativo. Un área INACTIVA no participa en las evaluaciones de acceso concedido
    /// (Historia 8, paso 4).
    /// </summary>
    public Estado Estado { get; set; } = Estado.ACTIVO;

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }
}
