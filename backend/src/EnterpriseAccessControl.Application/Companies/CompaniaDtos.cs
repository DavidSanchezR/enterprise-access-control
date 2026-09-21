using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Companies;

/// <summary>contracts/companies.yaml — Compania.</summary>
public sealed record CompaniaDto(
    Guid Id,
    string Nombre,
    Guid TipoDocumentoId,
    string NumeroDocumento,
    TipoCompania TipoCompania,
    Estado Estado,
    string? ZonaHorariaIana);

/// <summary>contracts/companies.yaml — CompaniaRequest.</summary>
/// <remarks>
/// <c>ZonaHorariaIana</c> es obligatoria cuando <c>TipoCompania = PRINCIPAL_MANDANTE</c> (RF-080) y
/// carece de uso funcional para una CONTRATISTA, que no posee áreas ni bloques horarios propios.
/// </remarks>
public sealed record CompaniaRequest(
    string Nombre,
    Guid TipoDocumentoId,
    string NumeroDocumento,
    TipoCompania TipoCompania,
    Estado Estado,
    string? ZonaHorariaIana);

/// <summary>Filtros de listado de compañías (contracts/companies.yaml).</summary>
public sealed record FiltroCompanias(Estado? Estado, TipoCompania? TipoCompania, string? Texto);

/// <summary>contracts/companies.yaml — RelacionContratistaPrincipal.</summary>
public sealed record RelacionContratistaPrincipalDto(
    Guid Id,
    Guid CompaniaContratistaId,
    Guid CompaniaPrincipalId,
    DateTime FechaHoraInicio,
    DateTime? FechaHoraFin);

/// <summary>contracts/companies.yaml — RelacionContratistaPrincipalRequest.</summary>
public sealed record RelacionContratistaPrincipalRequest(
    Guid CompaniaPrincipalId,
    DateTime FechaHoraInicio);
