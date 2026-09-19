using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Compañía, clasificada como PRINCIPAL_MANDANTE o CONTRATISTA (RF-006, RF-042).
/// </summary>
/// <remarks>
/// Se construye en la fase Foundational —y no en su propia historia de usuario— porque
/// <c>AlcanceUsuarioCompañía</c> (US1) la referencia como FK antes de que US2 implemente su
/// mantenimiento completo (ver tasks.md, tabla de Dependencies).
/// </remarks>
public class Compania : EntidadBase
{
    public required string Nombre { get; set; }

    public required Guid TipoDocumentoId { get; set; }

    public required string NumeroDocumento { get; set; }

    /// <summary>Clasificación explícita y obligatoria; no admite un valor "sin clasificar" (RF-042).</summary>
    public required TipoCompania TipoCompania { get; set; }

    public Estado Estado { get; set; } = Estado.ACTIVO;

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }
}
