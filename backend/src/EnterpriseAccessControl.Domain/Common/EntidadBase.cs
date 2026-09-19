namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Raíz común de toda entidad persistente: identidad UID/UUID autogenerada e inmutable
/// (Constitución, Principio II; RF-013) más los metadatos de auditoría (Principio III).
/// </summary>
/// <remarks>
/// El <see cref="Id"/> se genera en el momento de construcción con <c>Guid.CreateVersion7()</c>
/// (research.md §1): UUID v7 aporta monotonía temporal aproximada, lo que reduce la fragmentación
/// de índices frente a un GUID aleatorio, y está disponible antes del INSERT (a diferencia de
/// NEWSEQUENTIALID(), que ataría la generación a la base de datos).
/// </remarks>
public abstract class EntidadBase : IAuditable
{
    protected EntidadBase() => Id = GuidV7.Nuevo();

    public Guid Id { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedById { get; set; }

    public Guid? UpdatedById { get; set; }
}
