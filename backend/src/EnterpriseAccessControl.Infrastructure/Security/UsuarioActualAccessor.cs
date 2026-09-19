using System.Security.Claims;
using EnterpriseAccessControl.Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Adaptador que resuelve el usuario autenticado desde los claims del JWT de la request en curso
/// (research.md §3, §6).
/// </summary>
public sealed class UsuarioActualAccessor(IHttpContextAccessor httpContextAccessor) : IUsuarioActualAccessor
{
    public Guid? UsuarioId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            var valor =
                principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue("sub");

            // Fuera de una request autenticada (migraciones, seed, tareas de arranque) no hay
            // usuario: la auditoría registra null en lugar de inventar un identificador.
            return Guid.TryParse(valor, out var usuarioId) ? usuarioId : null;
        }
    }
}
