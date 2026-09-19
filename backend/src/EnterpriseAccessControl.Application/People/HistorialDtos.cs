using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.People;

/// <summary>contracts/people.yaml — AsignacionCompania.</summary>
public sealed record AsignacionCompaniaDto(
    Guid Id,
    Guid PersonaId,
    Guid CompaniaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin,
    EstadoPertenencia Estado,
    MotivoFinPertenencia? MotivoFin);

/// <summary>contracts/people.yaml — AsignacionCompaniaRequest.</summary>
public sealed record AsignacionCompaniaRequest(
    Guid CompaniaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin);

/// <summary>Cuerpo de <c>/finalizar</c>: fecha efectiva del cese, que puede ser futura (RF-064).</summary>
public sealed record FinalizarPertenenciaRequest(DateTime FechaHoraFin);

/// <summary>Cuerpo de <c>/renovar</c>: nueva fecha de fin, estrictamente posterior (RF-073).</summary>
public sealed record RenovarPertenenciaRequest(DateTime FechaHoraFin);

/// <summary>contracts/people.yaml — ContextoOperativo.</summary>
public sealed record ContextoOperativoDto(
    Guid Id,
    Guid PersonaId,
    Guid CompaniaPrincipalId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin,
    Estado Estado,
    MotivoFinRevocacion? MotivoFin,
    Guid? RevocadoPorPertenenciaId);

/// <summary>contracts/people.yaml — ContextoOperativoRequest.</summary>
public sealed record ContextoOperativoRequest(
    Guid CompaniaPrincipalId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin);

/// <summary>contracts/people.yaml — AsignacionUnidadOrganizativa.</summary>
public sealed record AsignacionUnidadOrganizativaDto(
    Guid Id,
    Guid PersonaId,
    Guid ContextoOperativoId,
    Guid UnidadOrganizativaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin,
    Estado Estado,
    MotivoFinRevocacion? MotivoFin,
    Guid? RevocadoPorPertenenciaId);

/// <summary>contracts/people.yaml — AsignacionUnidadOrganizativaRequest.</summary>
public sealed record AsignacionUnidadOrganizativaRequest(
    Guid UnidadOrganizativaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin);

/// <summary>contracts/people.yaml — AsignacionTipoPersona.</summary>
public sealed record AsignacionTipoPersonaDto(
    Guid Id,
    Guid PersonaId,
    Guid TipoPersonaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin,
    Estado Estado);

/// <summary>contracts/people.yaml — AsignacionTipoPersonaRequest.</summary>
public sealed record AsignacionTipoPersonaRequest(
    Guid TipoPersonaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin);

/// <summary>contracts/people.yaml — ContextoOperativoVigente.</summary>
public sealed record ContextoOperativoVigenteDto(
    Guid ContextoOperativoId,
    Guid CompaniaPrincipalId,
    Guid? UnidadOrganizativaVigenteId);

/// <summary>contracts/people.yaml — EstadoEfectivoPersona (RF-037).</summary>
public sealed record EstadoEfectivoPersonaDto(
    Guid PersonaId,
    DateTime FechaHoraEvaluada,
    Guid? CompaniaVigenteId,
    IReadOnlyList<ContextoOperativoVigenteDto> ContextosOperativosVigentes,
    IReadOnlyList<Guid> PerfilesVigentesIds);
