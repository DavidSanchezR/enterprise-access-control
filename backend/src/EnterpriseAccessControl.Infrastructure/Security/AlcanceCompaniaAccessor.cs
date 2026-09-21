using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Adaptador que resuelve el alcance administrativo efectivo desde los claims del JWT
/// (RF-074, RF-077; research.md §3, §27).
/// </summary>
/// <remarks>
/// El alcance se deriva del rol: un <c>GLOBAL_ADMINISTRATOR</c> responde afirmativamente para
/// cualquier compañía sin que el token enumere ninguna, mientras que un
/// <c>COMPANY_ADMINISTRATOR</c> se limita a las compañías de sus claims. Un token sin claim de rol
/// —emitido antes de esta sesión, o de un usuario sin asignación vigente— no tiene alcance alguno:
/// denegación por defecto (Principio I).
/// </remarks>
public sealed class AlcanceCompaniaAccessor(IHttpContextAccessor httpContextAccessor)
    : IAlcanceCompaniaAccessor
{
    public RolAdministrativo? Rol
    {
        get
        {
            var valor = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimsPersonalizados.Rol)?.Value;

            return Enum.TryParse<RolAdministrativo>(valor, ignoreCase: false, out var rol) ? rol : null;
        }
    }

    public bool EsGlobal => Rol == RolAdministrativo.GLOBAL_ADMINISTRATOR;

    public IReadOnlySet<Guid> CompaniaIds
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            if (principal is null || EsGlobal)
            {
                // El alcance GLOBAL no se enumera: devolver aquí "todas las compañías" obligaría a
                // consultarlas y daría una falsa sensación de lista cerrada.
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

    public bool TieneAlcanceVigente => EsGlobal || CompaniaIds.Count > 0;

    public bool EstaEnAlcance(Guid companiaId) => EsGlobal || CompaniaIds.Contains(companiaId);
}
