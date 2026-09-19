using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.OrgUnits;

/// <summary>contracts/org-units.yaml — UnidadOrganizativa.</summary>
/// <remarks>
/// <c>CompaniaPrincipalId</c> no es una columna de la entidad (RF-044): lo resuelve el servidor
/// subiendo hasta la raíz del árbol y consultando la entidad de enlace (RF-045).
/// </remarks>
public sealed record UnidadOrganizativaDto(
    Guid Id,
    string Nombre,
    Guid? UnidadSuperiorId,
    Guid CompaniaPrincipalId,
    Estado Estado);

/// <summary>contracts/org-units.yaml — UnidadOrganizativaRequest.</summary>
/// <remarks>
/// Los dos identificadores llevan valor por defecto para que el documento OpenAPI generado los
/// declare opcionales, igual que el contrato (<c>required: [nombre, estado]</c>). Sin el valor por
/// defecto, un parámetro posicional se publica como requerido y un generador de clientes estricto
/// obligaría a enviar <c>"unidadSuperiorId": null</c> al crear un nodo raíz.
///
/// Que <c>companiaPrincipalId</c> sea obligatorio <em>solo</em> cuando <c>unidadSuperiorId</c> es
/// null (RF-045) es una condicionalidad que JSON Schema no expresa aquí; la impone el servicio.
/// </remarks>
public sealed record UnidadOrganizativaRequest(
    string Nombre,
    Estado Estado,
    Guid? UnidadSuperiorId = null,
    Guid? CompaniaPrincipalId = null);

/// <summary>contracts/org-units.yaml — UnidadOrganizativaUpdateRequest.</summary>
public sealed record UnidadOrganizativaUpdateRequest(string Nombre, Estado Estado);

/// <summary>contracts/org-units.yaml — cuerpo de POST /mover.</summary>
public sealed record MoverUnidadRequest(Guid? NuevoPadreId);

/// <summary>contracts/org-units.yaml — NodoArbol.</summary>
public sealed record NodoArbolDto(
    Guid Id,
    string Nombre,
    Estado Estado,
    IReadOnlyList<NodoArbolDto> Hijos);
