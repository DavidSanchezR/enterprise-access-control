namespace EnterpriseAccessControl.Domain.Enums;

/// <summary>
/// Nivel al que se otorga un permiso de acceso (RF-020, RF-025).
/// </summary>
/// <remarks>
/// El orden de los miembros no es arbitrario: reproduce la precedencia PERSONA &gt;
/// UNIDAD_ORGANIZATIVA &gt; COMPANIA (RF-025), de modo que ordenar por este valor resuelve el
/// conflicto cuando varios niveles aplican a la vez. Aun así, la precedencia se aplica explícitamente
/// en <c>EvaluadorDeAcceso</c> y no se deja implícita en el orden del enum.
/// </remarks>
public enum AlcancePermiso
{
    PERSONA = 1,
    UNIDAD_ORGANIZATIVA = 2,
    COMPANIA = 3,
}

/// <summary>
/// Día de la semana de un bloque horario, en hora local <c>America/Lima</c> (RF-022).
/// </summary>
/// <remarks>
/// No se reutiliza <see cref="System.DayOfWeek"/> porque es un valor de negocio persistido: su
/// numeración empieza en domingo y el contrato lo publica en español
/// (contracts/permissions.yaml, <c>DiaSemana</c>).
/// </remarks>
public enum DiaSemana
{
    LUNES = 1,
    MARTES = 2,
    MIERCOLES = 3,
    JUEVES = 4,
    VIERNES = 5,
    SABADO = 6,
    DOMINGO = 7,
}

/// <summary>Resultado de una evaluación de acceso (contracts/access-evaluation.yaml).</summary>
public enum ResultadoEvaluacion
{
    CONCEDIDO = 1,
    DENEGADO = 2,
}

/// <summary>
/// Causa de una denegación de acceso (contracts/access-evaluation.yaml, research.md §7).
/// </summary>
/// <remarks>
/// Los once valores son parte del contrato público y están en correspondencia con los cortes del
/// algoritmo de 14 pasos. Varios pasos comparten motivo a propósito: el contrato agrupa bajo
/// <see cref="SIN_CONTEXTO_OPERATIVO_VIGENTE"/> tanto la ausencia de contexto como su pérdida de
/// legitimidad, y bajo <see cref="SIN_CREDENCIAL_VIGENTE"/> los cuatro casos de credencial no
/// vigente, sin distinguirlos en la respuesta.
/// </remarks>
public enum MotivoDenegacion
{
    PERSONA_NO_ENCONTRADA = 1,
    AREA_NO_ENCONTRADA = 2,
    FUERA_DE_ALCANCE_USUARIO = 3,
    SIN_CONTEXTO_OPERATIVO_VIGENTE = 4,
    RELACION_CONTRATISTA_PRINCIPAL_VENCIDA = 5,
    SIN_CREDENCIAL_VIGENTE = 6,
    AREA_INACTIVA = 7,
    PERFIL_NO_AUTORIZADO_EN_AREA = 8,
    SIN_PERMISO_APLICABLE = 9,
    PERMISO_FUERA_DE_VIGENCIA = 10,
    FUERA_DE_BLOQUE_HORARIO = 11,
}
