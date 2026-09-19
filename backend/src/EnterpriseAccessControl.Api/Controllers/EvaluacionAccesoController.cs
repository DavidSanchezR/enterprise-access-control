using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.Api.Controllers;

/// <summary>
/// Evaluación de acceso de una persona a un área en una fecha/hora dada
/// (contracts/access-evaluation.yaml, Historia 8).
/// </summary>
/// <remarks>
/// La política <c>CompaniaScope</c> autoriza la propia consulta —paso 1 del algoritmo, RF-005—, que
/// es una pregunta distinta de la que responde el cuerpo: si esa persona puede entrar a esa área.
/// Confundir ambas es justamente lo que research.md §18 descarta.
///
/// La respuesta es siempre 200 cuando la evaluación puede realizarse: un acceso denegado es un
/// resultado legítimo de la consulta, no un fallo de la petición.
/// </remarks>
[ApiController]
[Route("api/evaluacion-acceso")]
[Authorize(Policy = CompaniaScopeRequirement.PolicyName)]
public sealed class EvaluacionAccesoController(EvaluacionAccesoService evaluacion) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<EvaluarAccesoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluarAccesoResponse>> Evaluar(
        [FromBody] EvaluarAccesoRequest request,
        CancellationToken ct) =>
        Ok(await evaluacion.EvaluarAsync(request, ct));
}
