using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Credenciales/fotocheck de una persona por Compañía Principal (contracts/credentials.yaml, Historia 9).
/// </summary>
/// <remarks>
/// La eliminación es siempre baja lógica: <c>DELETE</c> cambia el estado a ELIMINADO y conserva la
/// fila (RF-018). La revocación automática (REVOCADA) no tiene endpoint: solo la produce la cascada al
/// finalizar la pertenencia (RF-061).
/// </remarks>
[ApiController]
[Route("api/personas/{personaId:guid}/credenciales")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class CredencialesController(CredencialService credenciales) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AsignacionCredencialDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AsignacionCredencialDto>>> Listar(
        Guid personaId,
        [FromQuery] Guid? companiaPrincipalId,
        CancellationToken ct) =>
        Ok(await credenciales.ListarAsync(personaId, companiaPrincipalId, ct));

    [HttpPost]
    [ProducesResponseType<AsignacionCredencialDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignacionCredencialDto>> Asignar(
        Guid personaId,
        [FromBody] AsignacionCredencialRequest request,
        CancellationToken ct)
    {
        var creada = await credenciales.AsignarAsync(personaId, request, ct);

        // No existe GET individual en el contrato: la ubicación apunta al histórico de la persona.
        return Created($"/api/personas/{personaId}/credenciales", creada);
    }

    [HttpPost("{id:guid}/devolver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Devolver(Guid personaId, Guid id, CancellationToken ct)
    {
        await credenciales.DevolverAsync(personaId, id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(Guid personaId, Guid id, CancellationToken ct)
    {
        await credenciales.EliminarAsync(personaId, id, ct);
        return NoContent();
    }
}
