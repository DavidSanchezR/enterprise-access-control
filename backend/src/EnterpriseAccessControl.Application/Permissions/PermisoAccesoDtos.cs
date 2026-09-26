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

/// <summary>contracts/permissions.yaml — PermisoAcceso (v2.0.0).</summary>
/// <remarks>
/// <c>FechaHora*Vigencia</c> son los instantes UTC efectivos que evalúa el paso 12: los calculados desde las
/// fechas civiles (RF-083) o los conservados de un permiso anterior a VF-004. <c>Fecha*Vigencia</c>,
/// <c>VigenciaEnDiasCompletos</c> y <c>ZonaHorariaIana</c> se derivan en cada lectura con la zona efectiva
/// actual de la Principal del área, sin persistirse (research.md §36.5–36.6).
/// </remarks>
public sealed record PermisoAccesoDto(
    Guid Id,
    Guid AreaAccesoId,
    AlcancePermiso Alcance,
    Guid? PersonaId,
    Guid? UnidadOrganizativaId,
    Guid? CompaniaId,
    DateTime FechaHoraInicioVigencia,
    DateTime FechaHoraFinVigencia,
    DateOnly FechaInicioVigencia,
    DateOnly FechaFinVigencia,
    bool VigenciaEnDiasCompletos,
    string ZonaHorariaIana,
    Estado Estado,
    IReadOnlyList<BloqueHorarioDto> BloquesHorarios);

/// <summary>contracts/permissions.yaml — PermisoAccesoRequest.</summary>
/// <remarks>
/// Los tres identificadores de sujeto llevan valor por defecto para que el documento OpenAPI los
/// declare opcionales, igual que el contrato: exactamente uno debe informarse, según el alcance, y
/// esa condicionalidad no puede expresarse en JSON Schema.
///
/// <c>FechaFinVigencia</c> sí es obligatoria y sin valor por defecto: RF-021 la exige desde el
/// spec original para los tres alcances (RF-071).
///
/// Desde v2.0.0 (VF-004, RF-083) la vigencia se recibe como dos fechas civiles (<c>format: date</c>), cada
/// una un día completo en la zona de la Compañía Principal del área. Los campos <c>date-time</c> de v1.x ya
/// no se aceptan: un cuerpo que solo los trae deja estas fechas en su valor por defecto y el validador lo
/// rechaza con 400.
/// </remarks>
public sealed record PermisoAccesoRequest(
    Guid AreaAccesoId,
    AlcancePermiso Alcance,
    DateOnly FechaInicioVigencia,
    DateOnly FechaFinVigencia,
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
