namespace EnterpriseAccessControl.Application.People;

/// <summary>contracts/people.yaml — Persona.</summary>
public sealed record PersonaDto(
    Guid Id,
    string Nombres,
    string Apellidos,
    DateOnly FechaNacimiento,
    Guid TipoDocumentoId,
    string NumeroDocumento,
    Guid GeneroId,
    string CorreoElectronico,
    Guid TipoSangreId,
    string ContactoEmergencia,
    string NumeroEmergencia);

/// <summary>contracts/people.yaml — PersonaRequest.</summary>
public sealed record PersonaRequest(
    string Nombres,
    string Apellidos,
    DateOnly FechaNacimiento,
    Guid TipoDocumentoId,
    string NumeroDocumento,
    Guid GeneroId,
    string CorreoElectronico,
    Guid TipoSangreId,
    string ContactoEmergencia,
    string NumeroEmergencia);

/// <summary>Filtros de búsqueda de personas (contracts/people.yaml).</summary>
public sealed record FiltroPersonas(string? Texto, Guid? TipoDocumentoId, Guid? CompaniaId);
