using EnterpriseAccessControl.Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Adaptador que resuelve el alcance de compañías del usuario autenticado desde los claims del JWT
/// (RF-004, RF-005; research.md §3).
/// </summary>
public sealed class AlcanceCompaniaAccessor(IHttpContextAccessor httpContextAccessor) : IAlcanceCompaniaAccessor
{
    public IReadOnlySet<Guid> CompaniaIds
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            if (principal is null)
            {
                return new HashSet<Guid>();
            }

            var ids = new HashSet<Guid>();

            foreach (var claim in principal.FindAll(ClaimsPersonalizados.AlcanceCompania))
            {
                if (Guid.TryParse(claim.Value, out var companiaId))
                {
                    ids.Add(companiaId);
                }
            }

            return ids;
        }
    }

    public bool EstaEnAlcance(Guid companiaId) => CompaniaIds.Contains(companiaId);
}
