using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>Credenciales de inicio de sesión (contracts/auth.yaml, LoginRequest).</summary>
public sealed record LoginRequest(string Correo, string Password);

/// <summary>Resultado de un login exitoso (contracts/auth.yaml, LoginResponse).</summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiraEn,
    bool RequiereCambioPassword,
    IReadOnlyList<Guid> AlcanceCompanias);

/// <summary>Cambio de contraseña del usuario autenticado (contracts/auth.yaml).</summary>
public sealed record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

/// <summary>Sesión vigente (contracts/auth.yaml, SesionActual).</summary>
public sealed record SesionActual(Guid UsuarioId, string Correo, IReadOnlyList<Guid> AlcanceCompanias);

/// <summary>Usuario expuesto por el mantenimiento (contracts/users.yaml, Usuario).</summary>
public sealed record UsuarioDto(
    Guid Id,
    string Correo,
    EstadoUsuario Estado,
    bool RequiereCambioPassword,
    IReadOnlyList<Guid> AlcanceCompanias);

/// <summary>Alta de usuario (contracts/users.yaml, CrearUsuarioRequest).</summary>
public sealed record CrearUsuarioRequest(
    string Correo,
    string PasswordInicial,
    IReadOnlyList<Guid> CompaniaIds);

/// <summary>Actualización de usuario (contracts/users.yaml, ActualizarUsuarioRequest).</summary>
public sealed record ActualizarUsuarioRequest(string Correo, EstadoUsuario Estado);

/// <summary>Reemplazo del alcance de compañías (contracts/users.yaml).</summary>
public sealed record ReemplazarAlcanceRequest(IReadOnlyList<Guid> CompaniaIds);
