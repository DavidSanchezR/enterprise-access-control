using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Enlace entre una Compañía PRINCIPAL_MANDANTE y el nodo raíz de su árbol organizativo (RF-045).
/// </summary>
/// <remarks>
/// Existe como entidad propia porque <see cref="UnidadOrganizativa"/> no puede referenciar a
/// <c>Compañía</c> (RF-044). Al concentrar la pertenencia en una sola fila por árbol, cambiarla es
/// un acto explícito sobre esta tabla y no un efecto colateral de editar un nodo cualquiera.
///
/// Dos invariantes que la base de datos no puede expresar por sí sola y valida la capa de
/// aplicación:
/// <list type="bullet">
///   <item><see cref="CompaniaId"/> debe referenciar una compañía <c>PRINCIPAL_MANDANTE</c>: una
///   CONTRATISTA no posee unidades organizativas (RF-045).</item>
///   <item><see cref="UnidadOrganizativaRaizId"/> debe referenciar un nodo con
///   <c>UnidadSuperiorId = null</c>: enlazar un nodo intermedio partiría el árbol en dos
///   pertenencias.</item>
/// </list>
/// </remarks>
public class CompaniaPrincipalUnidadOrganizativaRaiz : EntidadBase
{
    public required Guid CompaniaId { get; set; }

    /// <summary>
    /// Nodo raíz del árbol. Único en toda la tabla: un mismo árbol no puede pertenecer a dos
    /// Compañías Principales a la vez (RF-043).
    /// </summary>
    public required Guid UnidadOrganizativaRaizId { get; set; }
}
