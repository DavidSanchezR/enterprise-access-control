using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>Credenciales de inicio de sesión (contracts/auth.yaml, LoginRequest).</summary>
public sealed record LoginRequest(string Correo, string Password);

/// <summary>Resultado de un login exitoso (contracts/auth.yaml, LoginResponse).</summary>
/// <remarks>
/// <c>CompaniaIds</c> se informa únicamente para <c>COMPANY_ADMINISTRATOR</c>: el alcance de un
/// <c>GLOBAL_ADMINISTRATOR</c> es toda compañía y no se enumera (RF-074).
/// </remarks>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiraEn,
    bool RequiereCambioPassword,
    RolAdministrativo? Rol,
    IReadOnlyList<Guid> CompaniaIds);

/// <summary>Cambio de contraseña del usuario autenticado (contracts/auth.yaml).</summary>
public sealed record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

/// <summary>Sesión vigente (contracts/auth.yaml, SesionActual).</summary>
public sealed record SesionActual(
    Guid UsuarioId,
    string Correo,
    RolAdministrativo? Rol,
    IReadOnlyList<Guid> CompaniaIds);

/// <summary>Asignación de rol administrativo (contracts/users.yaml, AsignacionRolAdministrativo).</summary>
public sealed record AsignacionRolAdministrativoDto(
    Guid Id,
    Guid UsuarioId,
    RolAdministrativo Rol,
    Guid? CompaniaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin,
    bool Vigente);

/// <summary>Usuario expuesto por el mantenimiento (contracts/users.yaml, Usuario).</summary>
public sealed record UsuarioDto(
    Guid Id,
    string Correo,
    EstadoUsuario Estado,
    bool RequiereCambioPassword,
    IReadOnlyList<AsignacionRolAdministrativoDto> AsignacionesRol);

/// <summary>
/// Parte común de los dos requests que crean una asignación de rol (RF-074).
/// </summary>
/// <remarks>
/// Existe para que la regla fundamental de <c>CompañíaId</c> se valide una sola vez en el borde
/// HTTP, en lugar de duplicarse en dos validadores que podrían divergir.
/// </remarks>
public interface IAsignacionDeRol
{
    RolAdministrativo Rol { get; }

    Guid? CompaniaId { get; }
}

/// <summary>Alta de usuario con su primera asignación de rol (contracts/users.yaml).</summary>
public sealed record CrearUsuarioRequest(
    string Correo,
    string PasswordInicial,
    RolAdministrativo Rol,
    Guid? CompaniaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin) : IAsignacionDeRol;

/// <summary>Actualización de usuario (contracts/users.yaml, ActualizarUsuarioRequest).</summary>
public sealed record ActualizarUsuarioRequest(string Correo, EstadoUsuario Estado);

/// <summary>Nueva asignación de rol para un usuario existente (contracts/users.yaml, AsignarRolRequest).</summary>
/// <remarks>
/// Agrega una asignación; nunca reemplaza el conjunto existente. Es la diferencia deliberada con el
/// antiguo <c>ReemplazarAlcanceRequest</c>, que borraba el alcance previo sin dejar histórico.
/// </remarks>
public sealed record AsignarRolRequest(
    RolAdministrativo Rol,
    Guid? CompaniaId,
    DateTime FechaHoraInicio,
    DateTime FechaHoraFin) : IAsignacionDeRol;

/// <summary>Renovación de una asignación de rol vigente (RF-073, RF-075).</summary>
/// <remarks>
/// El campo se llama <c>FechaHoraFin</c> —y no <c>NuevaFechaHoraFin</c>— para que el cuerpo JSON sea
/// idéntico al de la renovación de pertenencia ya publicada en <c>contracts/people.yaml</c>: un cliente
/// no debería tener que recordar dos nombres para la misma operación (contracts/users.yaml v2.1.0).
/// </remarks>
public sealed record RenovarAsignacionRolRequest(DateTime FechaHoraFin);

/// <summary>
/// Filtros del listado de usuarios (contracts/users.yaml, <c>GET /api/usuarios</c>).
/// </summary>
/// <remarks>
/// <paramref name="Texto"/> busca por coincidencia parcial de correo y se resuelve en el servidor
/// sobre todo el conjunto dentro del alcance del solicitante, antes de paginar (RF-077, UX-22).
/// Simétrico a <c>FiltroCompanias</c>.
/// </remarks>
public sealed record FiltroUsuarios(EstadoUsuario? Estado, string? Texto);
