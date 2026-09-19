using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Permisos de acceso a áreas, con vigencia y bloques horarios (contracts/permissions.yaml).
/// </summary>
/// <remarks>
/// El alcance administrativo se evalúa contra la Compañía Principal propietaria del área del permiso
/// (RF-049), no contra el sujeto al que se otorga: se está configurando el acceso a un área ajena a
/// quien lo recibe.
/// </remarks>
[ApiController]
[Route("api/permisos")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class PermisosController(PermisoAccesoService permisos) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PaginaResponse<PermisoAccesoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResponse<PermisoAccesoDto>>> Listar(
        [FromQuery] Guid? areaAccesoId,
        [FromQuery] Guid? personaId,
        [FromQuery] Guid? unidadOrganizativaId,
        [FromQuery] Guid? companiaId,
        [FromQuery] Estado? estado,
        [FromQuery] int? pagina,
        [FromQuery(Name = "tamañoPagina")] int? tamañoPagina,
        CancellationToken ct) =>
        Ok(await permisos.ListarAsync(
            new FiltroPermisos(areaAccesoId, personaId, unidadOrganizativaId, companiaId, estado),
            new ParametrosPaginacion(pagina, tamañoPagina),
            ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PermisoAccesoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermisoAccesoDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await permisos.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<PermisoAccesoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermisoAccesoDto>> Crear(
        [FromBody] PermisoAccesoRequest request,
        CancellationToken ct)
    {
        var creado = await permisos.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PermisoAccesoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermisoAccesoDto>> Actualizar(
        Guid id,
        [FromBody] PermisoAccesoRequest request,
        CancellationToken ct) =>
        Ok(await permisos.ActualizarAsync(id, request, ct));
}
