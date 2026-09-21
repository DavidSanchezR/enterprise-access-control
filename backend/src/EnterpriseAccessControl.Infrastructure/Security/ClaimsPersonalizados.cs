namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Nombres de los claims propios emitidos en el JWT (research.md §3, §27).
/// </summary>
public static class ClaimsPersonalizados
{
    /// <summary>
    /// Rol administrativo vigente del usuario (<c>AsignaciónRolAdministrativo.Rol</c>, RF-074).
    /// </summary>
    /// <remarks>
    /// Se emite un único claim con el valor del catálogo cerrado. Es lo que permite representar el
    /// alcance GLOBAL sin enumerar compañías, algo imposible con el esquema anterior de "un claim
    /// por compañía".
    /// </remarks>
    public const string Rol = "rol";

    /// <summary>
    /// Claim repetido, uno por compañía administrada (RF-074, RF-077).
    /// </summary>
    /// <remarks>
    /// Se emite **solo** para <c>COMPANY_ADMINISTRATOR</c>: un <c>GLOBAL_ADMINISTRATOR</c> no
    /// enumera compañías, su alcance lo determina el claim de rol. Viaja en el token para evitar una
    /// consulta a la base de datos en cada request solo para resolver el alcance.
    /// </remarks>
    public const string AlcanceCompania = "alcance_compania";
}
