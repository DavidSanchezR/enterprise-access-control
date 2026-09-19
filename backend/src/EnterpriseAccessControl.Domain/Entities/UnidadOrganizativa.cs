using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Nodo de la jerarquía organizativa de una Compañía Principal (RF-007, RF-008).
/// </summary>
/// <remarks>
/// **No tiene ninguna columna ni clave foránea hacia <c>Compañía</c>** (RF-044). La pertenencia a una
/// Compañía Principal se establece exclusivamente sobre el nodo raíz, mediante la entidad de enlace
/// <see cref="CompaniaPrincipalUnidadOrganizativaRaiz"/>; los nodos hijos la heredan recorriendo
/// <see cref="UnidadSuperiorId"/> hasta la raíz (RF-045).
///
/// Modelarlo así —en lugar de añadir un <c>CompañíaId</c> a cada nodo— evita que un nodo pueda
/// declarar una compañía distinta a la de su árbol, un estado inconsistente que ninguna restricción
/// declarativa podría impedir si la columna existiera.
///
/// La ausencia de ciclos (Principio V, RF-038) se valida en el servidor con un CTE recursivo antes
/// de confirmar cualquier creación o reubicación.
/// </remarks>
public class UnidadOrganizativa : EntidadBase
{
    public required string Nombre { get; set; }

    /// <summary>Padre en la jerarquía; <c>null</c> identifica un nodo raíz.</summary>
    public Guid? UnidadSuperiorId { get; set; }

    public Estado Estado { get; set; } = Estado.ACTIVO;

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }
}
