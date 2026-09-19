using EnterpriseAccessControl.Application.AreaAccess;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Árbol de áreas físicas de acceso por Compañía Principal (contracts/area-access.yaml).
/// </summary>
/// <remarks>
/// <c>companiaPrincipalId</c> es obligatorio al listar y al pedir el árbol: cada Principal tiene su
/// propio árbol aislado (RF-043, RF-046), de modo que no existe una consulta "global" de áreas que
/// tenga sentido.
/// </remarks>
[ApiController]
[Route("api/areas-acceso")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class AreasAccesoController(AreaAccesoService areas) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AreaAccesoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AreaAccesoDto>>> Listar(
        [FromQuery][BindRequired] Guid companiaPrincipalId,
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await areas.ListarAsync(companiaPrincipalId, estado, ct));

    [HttpGet("arbol")]
    [ProducesResponseType<IReadOnlyList<NodoAreaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NodoAreaDto>>> Arbol(
        [FromQuery][BindRequired] Guid companiaPrincipalId,
        CancellationToken ct) =>
        Ok(await areas.ObtenerArbolAsync(companiaPrincipalId, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AreaAccesoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AreaAccesoDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await areas.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<AreaAccesoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AreaAccesoDto>> Crear(
        [FromBody] AreaAccesoRequest request,
        CancellationToken ct)
    {
        var creada = await areas.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AreaAccesoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AreaAccesoDto>> Actualizar(
        Guid id,
        [FromBody] AreaAccesoUpdateRequest request,
        CancellationToken ct) =>
        Ok(await areas.ActualizarAsync(id, request, ct));

    [HttpPost("{id:guid}/mover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Mover(
        Guid id,
        [FromBody] MoverAreaRequest request,
        CancellationToken ct)
    {
        await areas.MoverAsync(id, request, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/tipos-persona")]
    [ProducesResponseType<IReadOnlyList<Guid>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Guid>>> TiposPersona(Guid id, CancellationToken ct) =>
        Ok(await areas.ObtenerTiposPersonaAsync(id, ct));

    [HttpPut("{id:guid}/tipos-persona")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReemplazarTiposPersona(
        Guid id,
        [FromBody] ReemplazarTiposPersonaRequest request,
        CancellationToken ct)
    {
        await areas.ReemplazarTiposPersonaAsync(id, request, ct);
        return NoContent();
    }
}
