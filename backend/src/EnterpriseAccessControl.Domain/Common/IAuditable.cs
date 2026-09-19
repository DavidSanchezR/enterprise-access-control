namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Metadatos de auditoría obligatorios en toda entidad persistente (Constitución, Principio III;
/// RF-026, RF-027). Son estampados exclusivamente por el servidor mediante el
/// SaveChangesInterceptor de Infraestructura — nunca se aceptan desde el cliente ni se exponen
/// como campos editables en los DTOs de entrada.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }

    DateTime UpdatedAt { get; set; }

    Guid? CreatedById { get; set; }

    Guid? UpdatedById { get; set; }
}
