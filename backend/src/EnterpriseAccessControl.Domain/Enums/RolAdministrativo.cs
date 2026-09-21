namespace EnterpriseAccessControl.Domain.Enums;

/// <summary>
/// Catálogo cerrado de roles administrativos (RF-074).
/// </summary>
/// <remarks>
/// Es un enum de dominio y no un dato maestro versionado: agregar un rol exige modificar el modelo
/// de autorización, no insertar una fila. Mismo patrón que <c>EstadoUsuario</c> y
/// <c>TipoCompania</c> (research.md §17, §27).
///
/// Cada valor determina la forma del alcance: <c>GLOBAL_ADMINISTRATOR</c> alcanza todas las
/// compañías sin enumerarlas y exige <c>CompaniaId = NULL</c>; <c>COMPANY_ADMINISTRATOR</c> exige
/// una <c>CompaniaId</c> concreta y queda limitado a ella (RF-074, regla fundamental).
/// </remarks>
public enum RolAdministrativo
{
    GLOBAL_ADMINISTRATOR = 1,
    COMPANY_ADMINISTRATOR = 2,
}
