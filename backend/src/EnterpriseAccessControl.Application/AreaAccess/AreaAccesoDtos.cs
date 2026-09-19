using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.AreaAccess;

/// <summary>contracts/area-access.yaml — AreaAcceso.</summary>
public sealed record AreaAccesoDto(
    Guid Id,
    string Nombre,
    Guid? AreaSuperiorId,
    Guid CompaniaPrincipalId,
    Estado Estado);

/// <summary>contracts/area-access.yaml — AreaAccesoRequest.</summary>
/// <remarks>
/// Los dos identificadores llevan valor por defecto para que el documento OpenAPI los declare
/// opcionales, igual que el contrato (<c>required: [nombre, estado]</c>).
///
/// <c>companiaPrincipalId</c> es obligatorio **solo** cuando <c>areaSuperiorId</c> es null: un área
/// hija hereda el de su padre. Esa condicionalidad no puede expresarse en JSON Schema, así que la
/// impone el servicio.
/// </remarks>
public sealed record AreaAccesoRequest(
    string Nombre,
    Estado Estado,
    Guid? AreaSuperiorId = null,
    Guid? CompaniaPrincipalId = null);

/// <summary>contracts/area-access.yaml — AreaAccesoUpdateRequest.</summary>
public sealed record AreaAccesoUpdateRequest(string Nombre, Estado Estado);

/// <summary>contracts/area-access.yaml — cuerpo de POST /mover.</summary>
public sealed record MoverAreaRequest(Guid? NuevoPadreId);

/// <summary>contracts/area-access.yaml — NodoArbol.</summary>
public sealed record NodoAreaDto(
    Guid Id,
    string Nombre,
    Estado Estado,
    IReadOnlyList<NodoAreaDto> Hijos);

/// <summary>contracts/area-access.yaml — cuerpo de PUT /{id}/tipos-persona (RF-019).</summary>
/// <remarks>
/// El conjunto se reemplaza entero en lugar de ofrecer altas y bajas sueltas: el contrato lo define
/// así y, además, evita que dos ediciones concurrentes dejen el área con una mezcla que nadie pidió.
/// Una lista vacía es una petición legítima —el área deja de admitir ningún tipo—, no un error.
/// </remarks>
public sealed record ReemplazarTiposPersonaRequest(IReadOnlyList<Guid> TipoPersonaIds);
