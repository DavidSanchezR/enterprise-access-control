namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Generador de identificadores UUID v7 (research.md §1, Constitución Principio II).
/// </summary>
/// <remarks>
/// Centralizado en un único punto para que la decisión sea auditable y sustituible: los
/// identificadores nunca se derivan de atributos de negocio mutables ni se aceptan del cliente.
/// </remarks>
public static class GuidV7
{
    public static Guid Nuevo() => Guid.CreateVersion7();
}
