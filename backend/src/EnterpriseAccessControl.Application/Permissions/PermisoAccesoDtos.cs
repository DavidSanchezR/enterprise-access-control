using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Permissions;

/// <summary>contracts/permissions.yaml — BloqueHorario.</summary>
/// <remarks>
/// Las horas viajan como <c>HH:mm</c> en hora local <c>America/Lima</c>, no como instantes: el
/// contrato lo declara así con un patrón explícito, y convertirlas a UTC en el transporte perdería
/// justo lo que el bloque significa.
/// </remarks>
public sealed record BloqueHorarioDto(Guid Id, DiaSemana DiaSemana, string HoraInicio, string HoraFin);

/// <summary>contracts/permissions.yaml — BloqueHorario en la petición (sin <c>id</c>).</summary>
public sealed record BloqueHorarioRequest(DiaSemana DiaSemana, string HoraInicio, string HoraFin);

/// <summary>contracts/permissions.yaml — PermisoAcceso.</summary>
public sealed record PermisoAccesoDto(
    Guid Id,
    Guid AreaAccesoId,
    AlcancePermiso Alcance,
    Guid? PersonaId,
    Guid? UnidadOrganizativaId,
    Guid? CompaniaId,
    DateTime FechaHoraInicioVigencia,
    DateTime FechaHoraFinVigencia,
    Estado Estado,
    IReadOnlyList<BloqueHorarioDto> BloquesHorarios);

/// <summary>contracts/permissions.yaml — PermisoAccesoRequest.</summary>
/// <remarks>
/// Los tres identificadores de sujeto llevan valor por defecto para que el documento OpenAPI los
/// declare opcionales, igual que el contrato: exactamente uno debe informarse, según el alcance, y
/// esa condicionalidad no puede expresarse en JSON Schema.
///
/// <c>FechaHoraFinVigencia</c> sí es obligatoria y sin valor por defecto: RF-021 la exige desde el
/// spec original para los tres alcances (RF-071).
/// </remarks>
public sealed record PermisoAccesoRequest(
    Guid AreaAccesoId,
    AlcancePermiso Alcance,
    DateTime FechaHoraInicioVigencia,
    DateTime FechaHoraFinVigencia,
    Estado Estado,
    IReadOnlyList<BloqueHorarioRequest> BloquesHorarios,
    Guid? PersonaId = null,
    Guid? UnidadOrganizativaId = null,
    Guid? CompaniaId = null);

/// <summary>Filtros del listado de permisos (contracts/permissions.yaml).</summary>
public sealed record FiltroPermisos(
    Guid? AreaAccesoId = null,
    Guid? PersonaId = null,
    Guid? UnidadOrganizativaId = null,
    Guid? CompaniaId = null,
    Estado? Estado = null);
