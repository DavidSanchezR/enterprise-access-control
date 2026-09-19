namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Nombres de los claims propios emitidos en el JWT (research.md §3).
/// </summary>
public static class ClaimsPersonalizados
{
    /// <summary>
    /// Claim repetido, uno por compañía dentro del alcance administrativo del usuario
    /// (<c>AlcanceUsuarioCompañía</c>, RF-004). Se emite en el token para evitar una consulta a la
    /// base de datos en cada request solo para resolver el alcance.
    /// </summary>
    public const string AlcanceCompania = "alcance_compania";
}
