using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Mantenimiento de usuarios y de su alcance de compañías (contracts/users.yaml).
/// </summary>
/// <remarks>
/// Protegido con la política <c>CompaniaScope</c>: solo un usuario con alcance administrativo puede
/// operar aquí (RF-005, CS-004).
/// </remarks>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class UsuariosController(UsuarioService usuarios) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PaginaResponse<UsuarioDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResponse<UsuarioDto>>> Listar(
        [FromQuery] EstadoUsuario? estado,
        [FromQuery] int? pagina,
        [FromQuery(Name = "tamañoPagina")] int? tamañoPagina,
        CancellationToken ct) =>
        Ok(await usuarios.ListarAsync(estado, new ParametrosPaginacion(pagina, tamañoPagina), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await usuarios.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Crear(
        [FromBody] CrearUsuarioRequest request,
        CancellationToken ct)
    {
        var creado = await usuarios.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Actualizar(
        Guid id,
        [FromBody] ActualizarUsuarioRequest request,
        CancellationToken ct) =>
        Ok(await usuarios.ActualizarAsync(id, request, ct));

    [HttpPost("{id:guid}/desbloquear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken ct)
    {
        await usuarios.DesbloquearAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/alcance-companias")]
    [ProducesResponseType<IReadOnlyList<Guid>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Guid>>> ObtenerAlcance(Guid id, CancellationToken ct) =>
        Ok(await usuarios.ObtenerAlcanceAsync(id, ct));

    [HttpPut("{id:guid}/alcance-companias")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReemplazarAlcance(
        Guid id,
        [FromBody] ReemplazarAlcanceRequest request,
        CancellationToken ct)
    {
        await usuarios.ReemplazarAlcanceAsync(id, request, ct);
        return NoContent();
    }
}
