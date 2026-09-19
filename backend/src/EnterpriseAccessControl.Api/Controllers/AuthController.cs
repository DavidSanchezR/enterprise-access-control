using System.Security.Claims;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Common.Errores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>Autenticación: login, cambio de contraseña y sesión vigente (contracts/auth.yaml).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AutenticacionService autenticacion) : ControllerBase
{
    /// <summary>Inicia sesión con correo y contraseña (RF-001). Sin MFA en esta fase.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var respuesta = await autenticacion.LoginAsync(request, ct);
        return Ok(respuesta);
    }

    /// <summary>Cambia la contraseña del usuario autenticado (RF-003).</summary>
    [HttpPost("cambiar-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CambiarPassword(
        [FromBody] CambiarPasswordRequest request,
        CancellationToken ct)
    {
        await autenticacion.CambiarPasswordAsync(UsuarioAutenticadoId(), request, ct);
        return NoContent();
    }

    /// <summary>Devuelve el usuario autenticado y su alcance de compañías vigente.</summary>
    [HttpGet("sesion")]
    [Authorize]
    [ProducesResponseType<SesionActual>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SesionActual>> Sesion(CancellationToken ct) =>
        Ok(await autenticacion.ObtenerSesionAsync(UsuarioAutenticadoId(), ct));

    private Guid UsuarioAutenticadoId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        return Guid.TryParse(valor, out var id)
            ? id
            : throw new ErrorNegocioException(
                "SESION_INVALIDA",
                "El token no contiene un identificador de usuario válido.",
                StatusCodes.Status401Unauthorized);
    }
}
