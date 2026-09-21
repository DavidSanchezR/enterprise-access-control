using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Compañía, clasificada como PRINCIPAL_MANDANTE o CONTRATISTA (RF-006, RF-042).
/// </summary>
/// <remarks>
/// Se construye en la fase Foundational —y no en su propia historia de usuario— porque
/// <c>AsignaciónRolAdministrativo</c> (US1) la referencia como FK antes de que US2 implemente su
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

    /// <summary>
    /// Zona horaria propia, como identificador IANA (RF-080; p. ej. <c>America/Lima</c>).
    /// </summary>
    /// <remarks>
    /// Obligatoria cuando <see cref="TipoCompania"/> es PRINCIPAL_MANDANTE: es la zona en la que se
    /// interpretan los bloques horarios de los permisos de sus áreas y se presentan sus fechas. Sin
    /// uso funcional para una CONTRATISTA, que no posee áreas ni bloques horarios propios.
    ///
    /// Los instantes se siguen persistiendo siempre en UTC (Principio IV): cambiar esta zona altera
    /// la representación local futura, nunca los instantes ya almacenados.
    /// </remarks>
    public string? ZonaHorariaIana { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }
}
