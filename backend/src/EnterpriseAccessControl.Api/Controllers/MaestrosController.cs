using EnterpriseAccessControl.Application.Masters;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Mantenimiento de los cinco catálogos maestros (contracts/masters.yaml).
/// </summary>
/// <remarks>
/// Cada catálogo conserva su propia ruta —el contrato las declara por separado y un cliente no
/// debería tener que conocer un nombre de catálogo como parámetro— pero las tres operaciones delegan
/// en el mismo servicio genérico, de modo que la lógica existe una sola vez.
///
/// Los catálogos son globales y no pertenecen a ninguna compañía, así que no se filtran por alcance;
/// sí se exige alcance administrativo para tocarlos.
/// </remarks>
[ApiController]
[Route("api/maestros")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class MaestrosController(IServiceProvider proveedor) : ControllerBase
{
    private MasterDataService<TEntity> Servicio<TEntity>()
        where TEntity : EntidadMaestra, new() =>
        proveedor.GetRequiredService<MasterDataService<TEntity>>();

    private Task<IReadOnlyList<MasterItem>> ListarAsync<TEntity>(Estado? estado, CancellationToken ct)
        where TEntity : EntidadMaestra, new() =>
        Servicio<TEntity>().ListarAsync(estado, ct);

    private async Task<ActionResult<MasterItem>> CrearAsync<TEntity>(
        string ruta,
        MasterItemRequest request,
        CancellationToken ct)
        where TEntity : EntidadMaestra, new()
    {
        var creado = await Servicio<TEntity>().CrearAsync(request, ct);

        // El contrato no expone GET por id para los catálogos: se devuelve la ubicación de la
        // colección, que es donde el recurso queda visible.
        return Created($"/api/maestros/{ruta}", creado);
    }

    private Task<MasterItem> ActualizarAsync<TEntity>(
        Guid id,
        MasterItemRequest request,
        CancellationToken ct)
        where TEntity : EntidadMaestra, new() =>
        Servicio<TEntity>().ActualizarAsync(id, request, ct);

    // --- Tipos de documento -----------------------------------------------------------------

    [HttpGet("tipos-documento")]
    [ProducesResponseType<IReadOnlyList<MasterItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterItem>>> ListarTiposDocumento(
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await ListarAsync<TipoDocumento>(estado, ct));

    [HttpPost("tipos-documento")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status201Created)]
    public Task<ActionResult<MasterItem>> CrearTipoDocumento(
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        CrearAsync<TipoDocumento>("tipos-documento", request, ct);

    [HttpPut("tipos-documento/{id:guid}")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MasterItem>> ActualizarTipoDocumento(
        Guid id,
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        Ok(await ActualizarAsync<TipoDocumento>(id, request, ct));

    // --- Tipos de sangre --------------------------------------------------------------------

    [HttpGet("tipos-sangre")]
    [ProducesResponseType<IReadOnlyList<MasterItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterItem>>> ListarTiposSangre(
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await ListarAsync<TipoSangre>(estado, ct));

    [HttpPost("tipos-sangre")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status201Created)]
    public Task<ActionResult<MasterItem>> CrearTipoSangre(
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        CrearAsync<TipoSangre>("tipos-sangre", request, ct);

    [HttpPut("tipos-sangre/{id:guid}")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterItem>> ActualizarTipoSangre(
        Guid id,
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        Ok(await ActualizarAsync<TipoSangre>(id, request, ct));

    // --- Géneros ----------------------------------------------------------------------------

    [HttpGet("generos")]
    [ProducesResponseType<IReadOnlyList<MasterItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterItem>>> ListarGeneros(
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await ListarAsync<Genero>(estado, ct));

    [HttpPost("generos")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status201Created)]
    public Task<ActionResult<MasterItem>> CrearGenero(
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        CrearAsync<Genero>("generos", request, ct);

    [HttpPut("generos/{id:guid}")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterItem>> ActualizarGenero(
        Guid id,
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        Ok(await ActualizarAsync<Genero>(id, request, ct));

    // --- Tipos de persona -------------------------------------------------------------------

    [HttpGet("tipos-persona")]
    [ProducesResponseType<IReadOnlyList<MasterItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterItem>>> ListarTiposPersona(
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await ListarAsync<TipoPersona>(estado, ct));

    [HttpPost("tipos-persona")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status201Created)]
    public Task<ActionResult<MasterItem>> CrearTipoPersona(
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        CrearAsync<TipoPersona>("tipos-persona", request, ct);

    [HttpPut("tipos-persona/{id:guid}")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterItem>> ActualizarTipoPersona(
        Guid id,
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        Ok(await ActualizarAsync<TipoPersona>(id, request, ct));

    // --- Tipos de credencial ----------------------------------------------------------------

    [HttpGet("tipos-credencial")]
    [ProducesResponseType<IReadOnlyList<MasterItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterItem>>> ListarTiposCredencial(
        [FromQuery] Estado? estado,
        CancellationToken ct) =>
        Ok(await ListarAsync<TipoCredencial>(estado, ct));

    [HttpPost("tipos-credencial")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status201Created)]
    public Task<ActionResult<MasterItem>> CrearTipoCredencial(
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        CrearAsync<TipoCredencial>("tipos-credencial", request, ct);

    [HttpPut("tipos-credencial/{id:guid}")]
    [ProducesResponseType<MasterItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MasterItem>> ActualizarTipoCredencial(
        Guid id,
        [FromBody] MasterItemRequest request,
        CancellationToken ct) =>
        Ok(await ActualizarAsync<TipoCredencial>(id, request, ct));
}
