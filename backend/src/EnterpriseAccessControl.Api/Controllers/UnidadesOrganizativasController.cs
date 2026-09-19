using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Jerarquía de unidades organizativas por Compañía Principal (contracts/org-units.yaml).
/// </summary>
/// <remarks>
/// <c>companiaPrincipalId</c> es obligatorio al listar y al pedir el árbol: el sistema admite varias
/// Compañías Principales simultáneas, cada una con su árbol aislado (RF-043), de modo que no existe
/// una consulta "global" de unidades que tenga sentido.
/// </remarks>
[ApiController]
[Route("api/unidades-organizativas")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class UnidadesOrganizativasController(UnidadOrganizativaService unidades) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UnidadOrganizativaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UnidadOrganizativaDto>>> Listar(
        [FromQuery][BindRequired] Guid companiaPrincipalId,
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await unidades.ListarAsync(companiaPrincipalId, estado, ct));

    [HttpGet("arbol")]
    [ProducesResponseType<IReadOnlyList<NodoArbolDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NodoArbolDto>>> Arbol(
        [FromQuery][BindRequired] Guid companiaPrincipalId,
        CancellationToken ct) =>
        Ok(await unidades.ObtenerArbolAsync(companiaPrincipalId, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UnidadOrganizativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UnidadOrganizativaDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await unidades.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<UnidadOrganizativaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadOrganizativaDto>> Crear(
        [FromBody] UnidadOrganizativaRequest request,
        CancellationToken ct)
    {
        var creada = await unidades.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UnidadOrganizativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadOrganizativaDto>> Actualizar(
        Guid id,
        [FromBody] UnidadOrganizativaUpdateRequest request,
        CancellationToken ct) =>
        Ok(await unidades.ActualizarAsync(id, request, ct));

    [HttpPost("{id:guid}/mover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Mover(
        Guid id,
        [FromBody] MoverUnidadRequest request,
        CancellationToken ct)
    {
        await unidades.MoverAsync(id, request, ct);
        return NoContent();
    }
}
