namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Hashing y verificación de contraseñas (RF-003; Constitución: nunca en texto plano).
/// </summary>
/// <remarks>
/// Puerto propio para que la Aplicación no dependa directamente de
/// <c>PasswordHasher&lt;TUser&gt;</c> de ASP.NET Core Identity, cuyo parámetro genérico obligaría a
/// arrastrar un modelo de usuario ajeno al dominio (research.md §2).
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Verifica la contraseña contra el hash, en tiempo constante respecto al contenido.</summary>
    bool Verificar(string hash, string password);
}
