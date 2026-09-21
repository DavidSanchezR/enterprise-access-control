using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>Token de acceso emitido tras un login exitoso.</summary>
public sealed record TokenEmitido(string AccessToken, DateTime ExpiraEn);

/// <summary>
/// Emisión del JWT de acceso (research.md §2).
/// </summary>
/// <remarks>
/// El token incorpora el alcance administrativo para que el gate de autorización de primera línea no
/// tenga que consultar la base de datos en cada request (research.md §3). Desde la Sesión 2026-09-20
/// ese alcance se expresa como rol + compañías, y no como una lista plana: un
/// <c>GLOBAL_ADMINISTRATOR</c> no enumera compañías (RF-074).
/// </remarks>
public interface ITokenService
{
    /// <param name="rol">Rol vigente; <c>null</c> si el usuario no tiene ninguna asignación vigente.</param>
    /// <param name="companiaIds">
    /// Compañías administradas. Solo se emiten para <c>COMPANY_ADMINISTRATOR</c>; se ignoran para
    /// <c>GLOBAL_ADMINISTRATOR</c>, cuyo alcance no se enumera.
    /// </param>
    TokenEmitido Emitir(
        Guid usuarioId,
        string correo,
        RolAdministrativo? rol,
        IReadOnlyList<Guid> companiaIds);
}
