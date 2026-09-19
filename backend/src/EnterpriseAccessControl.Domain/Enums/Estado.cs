namespace EnterpriseAccessControl.Domain.Enums;

/// <summary>
/// Estado administrativo genérico de catálogos y entidades maestras (RF-032).
/// </summary>
/// <remarks>
/// Persistido como texto (<c>nvarchar</c>) mediante value conversion, nunca como entero, para que
/// el valor sea legible directamente en la base de datos y estable ante reordenamientos del enum
/// (research.md §17).
/// </remarks>
public enum Estado
{
    ACTIVO = 1,
    INACTIVO = 2,
}
