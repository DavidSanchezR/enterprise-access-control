using EnterpriseAccessControl.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Requisito de ASP.NET Core Authorization: el usuario autenticado debe tener al menos una
/// asignación de rol administrativo vigente (RF-005, RF-049, RF-060, RF-077).
/// </summary>
public sealed class CompaniaScopeRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "CompaniaScope";
}

/// <summary>
/// Gate de primera línea del alcance administrativo, a nivel de endpoint (research.md §18).
/// </summary>
/// <remarks>
/// **Distinción crítica del dominio**: esto NO es el motor de evaluación de acceso físico. Aquí se
/// responde "¿puede este <c>ClaimsPrincipal</c> invocar este endpoint?"; el acceso de una
/// <c>Persona</c> a un <c>ÁreaAcceso</c> lo decide <c>EvaluadorDeAcceso</c>, un servicio de dominio
/// de 15 pasos que evalúa una entidad de negocio, casi siempre distinta del usuario que dispara la
/// consulta. Modelar el segundo como AuthorizationPolicy mezclaría dos conceptos con ciclos de
/// vida y pruebas distintas (research.md §18).
///
/// Este handler solo verifica que exista alcance vigente. Desde la Sesión 2026-09-20 (D1) eso ya no
/// equivale a "tiene compañías enumeradas": un <c>GLOBAL_ADMINISTRATOR</c> no enumera ninguna y
/// seguiría siendo denegado bajo la comprobación anterior. La comprobación de que una compañía
/// *concreta* está dentro del alcance ocurre en cada caso de uso mediante
/// <see cref="IAlcanceCompaniaAccessor"/>, como defensa en profundidad (Principio I).
/// </remarks>
public sealed class CompaniaScopeAuthorizationHandler(IAlcanceCompaniaAccessor alcance)
    : AuthorizationHandler<CompaniaScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CompaniaScopeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated == true && alcance.TieneAlcanceVigente)
        {
            context.Succeed(requirement);
        }

        // Sin Succeed explícito la política falla: denegación por defecto (Principio I).
        return Task.CompletedTask;
    }
}
