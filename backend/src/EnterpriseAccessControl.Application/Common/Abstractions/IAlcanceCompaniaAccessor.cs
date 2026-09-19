namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Alcance de compañías administrables por el usuario autenticado (RF-004, RF-005, RF-049, RF-060).
/// </summary>
/// <remarks>
/// Segunda línea de defensa del Principio I: el <c>CompaniaScopeAuthorizationHandler</c> actúa como
/// gate de primera línea a nivel de endpoint, y este accessor alimenta el filtro de consulta de la
/// capa de datos. Un usuario nunca debe leer, listar ni modificar datos de una compañía fuera de su
/// alcance, incluso conociendo el identificador del recurso.
/// </remarks>
public interface IAlcanceCompaniaAccessor
{
    /// <summary>Compañías dentro del alcance del usuario autenticado; vacío si no hay usuario autenticado.</summary>
    IReadOnlySet<Guid> CompaniaIds { get; }

    /// <summary>Indica si la compañía indicada está dentro del alcance del usuario autenticado.</summary>
    bool EstaEnAlcance(Guid companiaId);
}
