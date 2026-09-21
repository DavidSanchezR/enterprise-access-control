using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Services;

/// <summary>Un permiso junto a sus bloques horarios, tal como se evalúa (RF-022).</summary>
public sealed record PermisoConBloques(
    PermisoAcceso Permiso,
    IReadOnlyList<BloqueHorarioPermiso> Bloques);

/// <summary>
/// Todo lo que <see cref="EvaluadorDeAcceso"/> necesita para decidir, ya resuelto.
/// </summary>
/// <remarks>
/// El evaluador es un servicio de dominio puro (research.md §18): no consulta la base de datos ni
/// conoce ASP.NET Core. Recibe aquí el estado completo —cargado por el orquestador de Aplicación— y
/// se limita a aplicar las reglas. Eso es lo que permite probar los 15 pasos uno a uno sin motor ni
/// <c>HttpContext</c> (Principio VII).
///
/// Todos los campos son <c>required</c> a propósito: un dato olvidado debe ser un error de
/// compilación y no una denegación silenciosa que parezca una regla de negocio.
/// </remarks>
public sealed record DatosDeEvaluacion
{
    /// <summary>Instante UTC evaluado; los bloques horarios se comparan en hora local (RF-022).</summary>
    public required DateTime FechaHoraUtc { get; init; }

    /// <summary>Paso 2: persona evaluada; <c>null</c> si no existe o está fuera de alcance.</summary>
    public required Guid? PersonaId { get; init; }

    /// <summary>Pasos 3, 4 y 8: el área evaluada, con su Compañía Principal y su estado.</summary>
    public required AreaAcceso? Area { get; init; }

    /// <summary>
    /// Pasos 5 y 13: la Compañía Principal propietaria del área, con su estado y su zona horaria
    /// (RF-079, RF-080).
    /// </summary>
    /// <remarks>
    /// Se pasa la entidad completa y no solo su identificador porque el algoritmo necesita dos
    /// cosas de ella —<c>Estado</c> para el paso 5 y <c>ZonaHorariaIana</c> para el paso 13— y
    /// resolverlas por separado abriría la puerta a evaluar una con datos de una Principal y la otra
    /// con los de otra.
    /// </remarks>
    public required Compania? CompaniaPrincipal { get; init; }

    /// <summary>
    /// Paso 1: el usuario que consulta tiene en su alcance la Principal propietaria del área
    /// (RF-005, RF-049).
    /// </summary>
    public required bool UsuarioTieneAlcanceSobrePrincipal { get; init; }

    /// <summary>Paso 6: contexto operativo vigente entre la persona y esa Principal (RF-059).</summary>
    public required ContextoOperativoPersonaPrincipal? ContextoOperativo { get; init; }

    /// <summary>
    /// Paso 6: compañía de pertenencia vigente de la persona en la fecha evaluada (RF-061, RF-065),
    /// con su estado (RF-079).
    /// </summary>
    /// <remarks>
    /// Se re-deriva en cada evaluación en lugar de confiar en el contexto ya escrito: es la defensa
    /// dinámica del paso 6, complementaria —no sustituta— de la revocación en cascada de US5.
    /// </remarks>
    public required Compania? CompaniaPertenencia { get; init; }

    /// <summary>
    /// Paso 6: la relación Contratista→Principal está vigente en la fecha evaluada (RF-054, RF-059).
    /// </summary>
    /// <remarks>Solo se consulta cuando la compañía de pertenencia es CONTRATISTA.</remarks>
    public required bool RelacionContratistaPrincipalVigente { get; init; }

    /// <summary>Paso 7: credencial de la persona para esa Principal, vigente o no (RF-066, RF-070).</summary>
    public required AsignacionCredencial? Credencial { get; init; }

    /// <summary>Paso 9: perfiles vigentes de la persona en la fecha evaluada (RF-011).</summary>
    public required IReadOnlyCollection<Guid> TiposPersonaVigentes { get; init; }

    /// <summary>Paso 9: perfiles que el área admite (RF-019, RF-024).</summary>
    public required IReadOnlyCollection<Guid> TiposPersonaAutorizadosEnArea { get; init; }

    /// <summary>Paso 10: unidad organizativa vigente dentro de ese contexto operativo (RF-055).</summary>
    public required Guid? UnidadOrganizativaVigenteId { get; init; }

    /// <summary>Paso 11: permisos del área en los tres niveles, sin filtrar.</summary>
    public required IReadOnlyCollection<PermisoConBloques> PermisosDelArea { get; init; }
}

/// <summary>Resultado de una evaluación y los datos que la explican.</summary>
/// <remarks>
/// <see cref="CompaniaPrincipalId"/> y <see cref="ContextoOperativoId"/> se informan también cuando
/// el resultado es DENEGADO (contracts/access-evaluation.yaml): sin ellos, quien administra no puede
/// saber contra qué Principal se evaluó y tendría que adivinar por qué falló.
/// </remarks>
public sealed record ResultadoDeEvaluacion(
    ResultadoEvaluacion Resultado,
    MotivoDenegacion? MotivoDenegacion,
    Guid? CompaniaPrincipalId,
    Guid? ContextoOperativoId,
    Guid? PermisoAplicadoId,
    AlcancePermiso? NivelAplicado);
