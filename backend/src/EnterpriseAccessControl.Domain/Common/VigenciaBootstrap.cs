namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Fecha de fin de la asignación de rol sembrada en el arranque (RF-078).
/// </summary>
/// <remarks>
/// **Excepción explícita y acotada a RF-071/RF-075**, que prohíben tanto <c>null</c> como una fecha
/// centinela en las vigencias del sistema. Aplica exclusivamente a la única asignación
/// <c>GLOBAL_ADMINISTRATOR</c> que crea la rutina de arranque, y no debe generalizarse a ninguna otra
/// asignación de rol ni a ninguna entidad ligada a una <c>Persona</c>.
///
/// No se expone como opción de configuración a propósito: hacerlo invitaría a reutilizarla, que es
/// justo lo que la excepción excluye. Si el administrador inicial debe tener una vigencia acotada,
/// se le crea una asignación normal y se finaliza ésta.
/// </remarks>
public static class VigenciaBootstrap
{
    /// <summary><c>2999-12-31T23:59:59Z</c>.</summary>
    public static readonly DateTime MaxValidityDate =
        new(2999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
}
