using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Companies;

/// <summary>contracts/companies.yaml — Compania.</summary>
public sealed record CompaniaDto(
    Guid Id,
    string Nombre,
    Guid TipoDocumentoId,
    string NumeroDocumento,
    TipoCompania TipoCompania,
    Estado Estado);

/// <summary>contracts/companies.yaml — CompaniaRequest.</summary>
public sealed record CompaniaRequest(
    string Nombre,
    Guid TipoDocumentoId,
    string NumeroDocumento,
    TipoCompania TipoCompania,
    Estado Estado);

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
