namespace EnterpriseAccessControl.Application.Common.Errores;

/// <summary>
/// Códigos de error de negocio expuestos en la extensión <c>codigo</c> de ProblemDetails.
/// </summary>
/// <remarks>
/// Son parte del contrato público con el frontend (contracts/*.yaml): cambiarlos rompe clientes,
/// por eso se centralizan aquí en lugar de escribirse como literales dispersos.
/// </remarks>
public static class CodigosError
{
    // --- Integridad temporal (Principio IV) ---

    /// <summary>Dos vigencias del mismo tipo se solapan dentro de la misma partición (RF-039).</summary>
    public const string SolapamientoVigencia = "SOLAPAMIENTO_VIGENCIA";

    /// <summary>Período inválido: la fecha de fin no es posterior a la de inicio (RF-039).</summary>
    public const string PeriodoInvalido = "PERIODO_INVALIDO";

    /// <summary>
    /// La asociación dependiente excede la vigencia de la AsignaciónPersonaCompañía que la sustenta
    /// (RF-072).
    /// </summary>
    public const string FueraDeContencionTemporal = "FUERA_DE_CONTENCION_TEMPORAL";

    /// <summary>Falta una fecha de fin obligatoria; no se admite null ni fecha centinela (RF-071).</summary>
    public const string FechaFinObligatoria = "FECHA_FIN_OBLIGATORIA";

    // --- Renovación de pertenencia (RF-073) ---

    /// <summary>La nueva fecha de fin no es estrictamente posterior a la vigente (RF-073).</summary>
    public const string RenovacionNoPosterior = "RENOVACION_NO_POSTERIOR";

    /// <summary>
    /// La pertenencia no es renovable: está FINALIZADA, o sigue ACTIVA pero ya expiró dinámicamente
    /// (RF-073, Decisión Pendiente #10 resuelta).
    /// </summary>
    public const string PertenenciaNoRenovable = "PERTENENCIA_NO_RENOVABLE";

    // --- Contexto operativo y relaciones (Historia 5) ---

    /// <summary>La Compañía Principal indicada no corresponde a la pertenencia de la persona (RF-053).</summary>
    public const string PrincipalNoCorrespondeAPertenencia = "PRINCIPAL_NO_CORRESPONDE_A_PERTENENCIA";

    /// <summary>No existe RelaciónContratistaPrincipal vigente con esa Principal (RF-054).</summary>
    public const string SinRelacionContratistaPrincipalVigente = "SIN_RELACION_CONTRATISTA_PRINCIPAL_VIGENTE";

    /// <summary>
    /// No existe un ContextoOperativoPersonaPrincipal vigente entre la persona y esa Principal (RF-056).
    /// </summary>
    public const string SinContextoOperativoVigente = "SIN_CONTEXTO_OPERATIVO_VIGENTE";

    /// <summary>La persona no tiene compañía de pertenencia vigente (RF-052, research.md §13).</summary>
    public const string SinPertenenciaVigente = "SIN_PERTENENCIA_VIGENTE";

    // --- Roles administrativos (RF-074 a RF-078) ---

    /// <summary>
    /// <c>CompañíaId</c> incompatible con el rol: debe ser NULL para GLOBAL_ADMINISTRATOR y
    /// obligatoria para COMPANY_ADMINISTRATOR (RF-074, regla fundamental).
    /// </summary>
    public const string RolCompaniaInconsistente = "ROL_COMPANIA_INCONSISTENTE";

    /// <summary>
    /// El solicitante no puede asignar ese rol o esa compañía (RF-076): un COMPANY_ADMINISTRATOR no
    /// asigna GLOBAL_ADMINISTRATOR, no se autoeleva y no administra otra compañía.
    /// </summary>
    public const string RolNoAutorizado = "ROL_NO_AUTORIZADO";

    /// <summary>La asignación de rol ya no está vigente y no admite finalización ni renovación (RF-075).</summary>
    public const string AsignacionRolNoVigente = "ASIGNACION_ROL_NO_VIGENTE";

    // --- Compañías (RF-080, RF-081) ---

    /// <summary>
    /// El cambio de <c>TipoCompañía</c> se rechaza porque existen dependencias incompatibles con el
    /// tipo destino; nunca se eliminan ni modifican en cascada (RF-081).
    /// </summary>
    public const string CambioTipoCompaniaConDependencias = "CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS";

    /// <summary>El identificador de zona horaria no es un IANA reconocido (RF-080).</summary>
    public const string ZonaHorariaInvalida = "ZONA_HORARIA_INVALIDA";

    /// <summary>Una Compañía Principal exige zona horaria IANA propia (RF-080).</summary>
    public const string ZonaHorariaRequerida = "ZONA_HORARIA_REQUERIDA";

    // --- Jerarquías (Principio V) ---

    /// <summary>La operación crearía un ciclo en la jerarquía (RF-038).</summary>
    public const string CicloJerarquico = "CICLO_JERARQUICO";

    /// <summary>Una unidad organizativa raíz o un área no puede asociarse a una CONTRATISTA (RF-045, RF-046).</summary>
    public const string CompaniaDebeSerPrincipal = "COMPANIA_DEBE_SER_PRINCIPAL";

    // --- Credenciales (Historia 9) ---

    /// <summary>
    /// La operación exige una credencial en estado ASIGNADO, el único no terminal
    /// (contracts/credentials.yaml, EstadoCredencial).
    /// </summary>
    public const string CredencialNoAsignada = "CREDENCIAL_NO_ASIGNADA";

    // --- Estado y concurrencia ---

    /// <summary>Un valor maestro INACTIVO no puede usarse en nuevas asignaciones (RF-032).</summary>
    public const string ValorMaestroInactivo = "VALOR_MAESTRO_INACTIVO";

    /// <summary>Otro usuario modificó el registro entre la lectura y la escritura (research.md §16).</summary>
    public const string ConflictoConcurrencia = "CONFLICTO_CONCURRENCIA";

    /// <summary>Recurso inexistente o fuera del alcance de compañías del usuario (RF-005).</summary>
    public const string RecursoNoEncontrado = "RECURSO_NO_ENCONTRADO";

    /// <summary>Error de validación de entrada (RF-033).</summary>
    public const string ValidacionEntrada = "VALIDACION_ENTRADA";
}
