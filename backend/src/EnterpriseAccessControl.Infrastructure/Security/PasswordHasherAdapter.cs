using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Adaptador sobre <see cref="PasswordHasher{TUser}"/> de ASP.NET Core Identity (research.md §2).
/// </summary>
/// <remarks>
/// Se reutiliza la implementación de Microsoft (PBKDF2 con parámetros mantenidos por el framework)
/// en lugar de escribir hashing propio o añadir una dependencia de terceros: cumple RF-003 sin
/// arrastrar el esquema de tablas de Identity, que no encaja con el modelo de <c>Usuario</c> de
/// este dominio (estados ACTIVO/INACTIVO/BLOQUEADO, alcance de compañías).
/// </remarks>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    // El hasher de Identity requiere una instancia de usuario, pero no la usa para derivar el hash
    // (la sal va dentro del propio hash). Se pasa un centinela para no acoplar el puerto a la entidad.
    private static readonly Usuario Centinela = new() { Correo = "-", PasswordHash = "-" };

    public string Hash(string password) => _hasher.HashPassword(Centinela, password);

    public bool Verificar(string hash, string password)
    {
        var resultado = _hasher.VerifyHashedPassword(Centinela, hash, password);

        // SuccessRehashNeeded significa que el hash es válido pero se generó con parámetros
        // antiguos; se acepta el login y el rehash se atenderá en el próximo cambio de contraseña.
        return resultado is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
