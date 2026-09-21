using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Mantenimiento de compañías y de sus relaciones Contratista↔Principal (contracts/companies.yaml).
/// </summary>
[ApiController]
[Route("api/companias")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class CompaniasController(
    CompaniaService companias,
    RelacionContratistaPrincipalService relaciones) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PaginaResponse<CompaniaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResponse<CompaniaDto>>> Listar(
        [FromQuery] Estado? estado,
        [FromQuery] TipoCompania? tipoCompania,
        [FromQuery] string? texto,
        [FromQuery] int? pagina,
        [FromQuery(Name = "tamañoPagina")] int? tamañoPagina,
        CancellationToken ct) =>
        Ok(await companias.ListarAsync(
            new FiltroCompanias(estado, tipoCompania, texto),
            new ParametrosPaginacion(pagina, tamañoPagina),
            ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompaniaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompaniaDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await companias.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<CompaniaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompaniaDto>> Crear(
        [FromBody] CompaniaRequest request,
        CancellationToken ct)
    {
        var creada = await companias.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CompaniaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    // 409 CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS: el tipo destino es incompatible con dependencias
    // existentes (RF-081).
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompaniaDto>> Actualizar(
        Guid id,
        [FromBody] CompaniaRequest request,
        CancellationToken ct) =>
        Ok(await companias.ActualizarAsync(id, request, ct));

    // --- Relaciones Contratista↔Principal (RF-051) ------------------------------------------

    [HttpGet("{contratistaId:guid}/relaciones-principales")]
    [ProducesResponseType<IReadOnlyList<RelacionContratistaPrincipalDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RelacionContratistaPrincipalDto>>> ListarRelaciones(
        Guid contratistaId,
        CancellationToken ct) =>
        Ok(await relaciones.ListarAsync(contratistaId, ct));

    [HttpPost("{contratistaId:guid}/relaciones-principales")]
    [ProducesResponseType<RelacionContratistaPrincipalDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RelacionContratistaPrincipalDto>> CrearRelacion(
        Guid contratistaId,
        [FromBody] RelacionContratistaPrincipalRequest request,
        CancellationToken ct)
    {
        var creada = await relaciones.CrearAsync(contratistaId, request, ct);

        return CreatedAtAction(
            nameof(ListarRelaciones),
            new { contratistaId },
            creada);
    }

    [HttpPost("{contratistaId:guid}/relaciones-principales/{id:guid}/finalizar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FinalizarRelacion(
        Guid contratistaId,
        Guid id,
        CancellationToken ct)
    {
        await relaciones.FinalizarAsync(contratistaId, id, ct);
        return NoContent();
    }
}
