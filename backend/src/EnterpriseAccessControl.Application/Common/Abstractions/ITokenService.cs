namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>Token de acceso emitido tras un login exitoso.</summary>
public sealed record TokenEmitido(string AccessToken, DateTime ExpiraEn);

/// <summary>
/// Emisión del JWT de acceso (research.md §2).
/// </summary>
/// <remarks>
/// El token incorpora el alcance de compañías como claims repetidos para que el gate de
/// autorización de primera línea no tenga que consultar la base de datos en cada request
/// (research.md §3).
/// </remarks>
public interface ITokenService
{
    TokenEmitido Emitir(Guid usuarioId, string correo, IReadOnlyList<Guid> alcanceCompanias);
}
