using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Permissions;

/// <summary>contracts/access-evaluation.yaml — EvaluarAccesoRequest.</summary>
public sealed record EvaluarAccesoRequest(Guid PersonaId, Guid AreaAccesoId, DateTime FechaHora);

/// <summary>contracts/access-evaluation.yaml — EvaluarAccesoResponse.</summary>
/// <remarks>
/// <c>companiaPrincipalId</c> y <c>contextoOperativoId</c> se informan también cuando el resultado es
/// DENEGADO: sin ellos, quien administra sabría que falló pero no contra qué Principal se evaluó.
/// </remarks>
public sealed record EvaluarAccesoResponse(
    ResultadoEvaluacion Resultado,
    MotivoDenegacion? MotivoDenegacion,
    Guid? CompaniaPrincipalId,
    Guid? ContextoOperativoId,
    Guid? PermisoAplicadoId,
    AlcancePermiso? NivelAplicado,
    string EvaluadoEnZonaHoraria);
