using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Registro y búsqueda de personas (contracts/people.yaml, Historia 4).
/// </summary>
/// <remarks>
/// Todas las respuestas quedan limitadas al alcance de compañías del usuario (RF-035). Una persona
/// fuera de alcance devuelve 404 y no 403: distinguirlos permitiría confirmar su existencia.
/// </remarks>
[ApiController]
[Route("api/personas")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class PersonasController(
    PersonaService personas,
    HistorialPersonaService historial,
    ContextoOperativoService contextos,
    AsignacionUnidadOrganizativaService unidades,
    EstadoEfectivoService estadoEfectivo) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PaginaResponse<PersonaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResponse<PersonaDto>>> Buscar(
        [FromQuery] string? texto,
        [FromQuery] Guid? tipoDocumentoId,
        [FromQuery] Guid? companiaId,
        [FromQuery] int? pagina,
        [FromQuery(Name = "tamañoPagina")] int? tamañoPagina,
        CancellationToken ct) =>
        Ok(await personas.BuscarAsync(
            new FiltroPersonas(texto, tipoDocumentoId, companiaId),
            new ParametrosPaginacion(pagina, tamañoPagina),
            ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PersonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonaDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await personas.ObtenerAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<PersonaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Crear(
        [FromBody] PersonaRequest request,
        CancellationToken ct)
    {
        var creada = await personas.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PersonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Actualizar(
        Guid id,
        [FromBody] PersonaRequest request,
        CancellationToken ct) =>
        Ok(await personas.ActualizarAsync(id, request, ct));

    // --- Histórico de pertenencia (RF-014, RF-061, RF-073) ----------------------------------

    [HttpGet("{id:guid}/historial-companias")]
    [ProducesResponseType<IReadOnlyList<AsignacionCompaniaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AsignacionCompaniaDto>>> ListarHistorial(
        Guid id,
        CancellationToken ct) =>
        Ok(await historial.ListarAsync(id, ct));

    [HttpPost("{id:guid}/historial-companias")]
    [ProducesResponseType<AsignacionCompaniaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignacionCompaniaDto>> CrearAsignacionCompania(
        Guid id,
        [FromBody] AsignacionCompaniaRequest request,
        CancellationToken ct)
    {
        var creada = await historial.CrearAsignacionAsync(id, request, ct);

        return CreatedAtAction(nameof(ListarHistorial), new { id }, creada);
    }

    /// <summary>Cese explícito: cierra la pertenencia y dispara la cascada de revocación (RF-061).</summary>
    [HttpPost("{id:guid}/historial-companias/{asignacionId:guid}/finalizar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FinalizarPertenencia(
        Guid id,
        Guid asignacionId,
        [FromBody] FinalizarPertenenciaRequest request,
        CancellationToken ct)
    {
        await historial.FinalizarAsync(id, asignacionId, request, ct);
        return NoContent();
    }

    /// <summary>Renovación: extiende la vigencia sin cerrar ni revocar nada (RF-073).</summary>
    [HttpPost("{id:guid}/historial-companias/{asignacionId:guid}/renovar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenovarPertenencia(
        Guid id,
        Guid asignacionId,
        [FromBody] RenovarPertenenciaRequest request,
        CancellationToken ct)
    {
        await historial.RenovarAsync(id, asignacionId, request, ct);
        return NoContent();
    }

    // --- Contextos operativos (RF-052 a RF-055) ---------------------------------------------

    [HttpGet("{id:guid}/contextos-operativos")]
    [ProducesResponseType<IReadOnlyList<ContextoOperativoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContextoOperativoDto>>> ListarContextos(
        Guid id,
        CancellationToken ct) =>
        Ok(await contextos.ListarAsync(id, ct));

    [HttpPost("{id:guid}/contextos-operativos")]
    [ProducesResponseType<ContextoOperativoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContextoOperativoDto>> AbrirContexto(
        Guid id,
        [FromBody] ContextoOperativoRequest request,
        CancellationToken ct)
    {
        var creado = await contextos.AbrirAsync(id, request, ct);

        return CreatedAtAction(nameof(ListarContextos), new { id }, creado);
    }

    [HttpGet("{id:guid}/contextos-operativos/{contextoId:guid}/unidad-organizativa")]
    [ProducesResponseType<IReadOnlyList<AsignacionUnidadOrganizativaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AsignacionUnidadOrganizativaDto>>> ListarUnidades(
        Guid id,
        Guid contextoId,
        CancellationToken ct) =>
        Ok(await unidades.ListarAsync(id, contextoId, ct));

    [HttpPost("{id:guid}/contextos-operativos/{contextoId:guid}/unidad-organizativa")]
    [ProducesResponseType<AsignacionUnidadOrganizativaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignacionUnidadOrganizativaDto>> AsignarUnidad(
        Guid id,
        Guid contextoId,
        [FromBody] AsignacionUnidadOrganizativaRequest request,
        CancellationToken ct)
    {
        var creada = await unidades.AsignarAsync(id, contextoId, request, ct);

        return CreatedAtAction(nameof(ListarUnidades), new { id, contextoId }, creada);
    }

    // --- Perfiles y estado efectivo (RF-011, RF-037) -----------------------------------------

    [HttpGet("{id:guid}/perfiles")]
    [ProducesResponseType<IReadOnlyList<AsignacionTipoPersonaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AsignacionTipoPersonaDto>>> ListarPerfiles(
        Guid id,
        CancellationToken ct) =>
        Ok(await estadoEfectivo.ListarPerfilesAsync(id, ct));

    [HttpPost("{id:guid}/perfiles")]
    [ProducesResponseType<AsignacionTipoPersonaDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AsignacionTipoPersonaDto>> AsignarPerfil(
        Guid id,
        [FromBody] AsignacionTipoPersonaRequest request,
        CancellationToken ct)
    {
        var creado = await estadoEfectivo.AsignarPerfilAsync(id, request, ct);

        return CreatedAtAction(nameof(ListarPerfiles), new { id }, creado);
    }

    [HttpGet("{id:guid}/estado-efectivo")]
    [ProducesResponseType<EstadoEfectivoPersonaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EstadoEfectivoPersonaDto>> EstadoEfectivo(
        Guid id,
        [FromQuery][BindRequired] DateTime fechaHora,
        CancellationToken ct) =>
        Ok(await estadoEfectivo.ObtenerAsync(id, fechaHora, ct));
}
