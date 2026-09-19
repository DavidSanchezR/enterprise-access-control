namespace EnterpriseAccessControl.Domain.Enums;

/// <summary>
/// Estado del usuario administrativo (RF-002).
/// </summary>
/// <remarks>
/// <c>BLOQUEADO</c> se distingue de <c>INACTIVO</c> a propósito: el bloqueo lo produce el sistema
/// automáticamente tras superar el umbral de intentos fallidos y se revierte con una acción
/// administrativa de desbloqueo, mientras que INACTIVO es una baja deliberada. Ninguno de los dos
/// puede iniciar sesión (Historia 1, criterio 2).
/// </remarks>
public enum EstadoUsuario
{
    ACTIVO = 1,
    INACTIVO = 2,
    BLOQUEADO = 3,
}
