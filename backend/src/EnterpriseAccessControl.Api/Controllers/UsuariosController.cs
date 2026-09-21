using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Mantenimiento de usuarios y de sus asignaciones de rol administrativo (contracts/users.yaml v2).
/// </summary>
/// <remarks>
/// Protegido con la política <c>CompaniaScope</c>: solo un usuario con una asignación de rol
/// vigente puede operar aquí (RF-005, RF-077, CS-004).
///
/// Los antiguos endpoints <c>alcance-companias</c> desaparecieron con el modelo RBAC: el alcance ya
/// no es una lista plana que se reemplaza, sino un conjunto de asignaciones con vigencia propia que
/// se agregan y finalizan individualmente (RF-074, D1).
/// </remarks>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class UsuariosController(UsuarioService usuarios) : ControllerBase
{
    /// <summary>
    /// Listado paginado dentro del alcance del solicitante (contracts/users.yaml, RF-077, UX-22).
    /// </summary>
    /// <remarks>
    /// <paramref name="texto"/> busca por correo en el servidor, sobre todo el conjunto autorizado y
    /// antes de paginar: no se limita a la página pedida. Sin coincidencias responde `200` con una
    /// página vacía, nunca `404`.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PaginaResponse<UsuarioDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResponse<UsuarioDto>>> Listar(
        [FromQuery] EstadoUsuario? estado,
        [FromQuery] string? texto,
        [FromQuery] int? pagina,
        [FromQuery(Name = "tamañoPagina")] int? tamañoPagina,
        CancellationToken ct) =>
        Ok(await usuarios.ListarAsync(
            new FiltroUsuarios(estado, texto),
            new ParametrosPaginacion(pagina, tamañoPagina),
            ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await usuarios.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    // --- Asignaciones de rol administrativo (RF-074 a RF-077) --------------------------------

    [HttpGet("{id:guid}/roles")]
    [ProducesResponseType<IReadOnlyList<AsignacionRolAdministrativoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AsignacionRolAdministrativoDto>>> ListarRoles(
        Guid id,
        CancellationToken ct) =>
        Ok(await usuarios.ListarRolesAsync(id, ct));

    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType<AsignacionRolAdministrativoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignacionRolAdministrativoDto>> AsignarRol(
        Guid id,
        [FromBody] AsignarRolRequest request,
        CancellationToken ct)
    {
        var creada = await usuarios.AsignarRolAsync(id, request, ct);

        return CreatedAtAction(nameof(ListarRoles), new { id }, creada);
    }

    [HttpPost("{id:guid}/roles/{asignacionId:guid}/finalizar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FinalizarRol(
        Guid id,
        Guid asignacionId,
        CancellationToken ct)
    {
        await usuarios.FinalizarRolAsync(id, asignacionId, ct);
        return NoContent();
    }

    /// <summary>
    /// Extiende la vigencia de una asignación de rol vigente (RF-075; contracts/users.yaml v2.1.0).
    /// </summary>
    /// <remarks>
    /// Contraparte inversa de <c>finalizar</c>. Toda la regla vive en el dominio
    /// (<c>AsignacionRolAdministrativoService.RenovarAsync</c>) y la autorización y el alcance en
    /// <c>UsuarioService.RenovarRolAsync</c>: aquí no se duplica ninguna validación. Responde `204`
    /// aunque el servicio devuelva la asignación renovada, igual que <c>finalizar</c> y que la
    /// renovación de pertenencia de <c>contracts/people.yaml</c>.
    /// </remarks>
    [HttpPost("{id:guid}/roles/{asignacionId:guid}/renovar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenovarRol(
        Guid id,
        Guid asignacionId,
        [FromBody] RenovarAsignacionRolRequest request,
        CancellationToken ct)
    {
        await usuarios.RenovarRolAsync(id, asignacionId, request, ct);
        return NoContent();
    }
}
