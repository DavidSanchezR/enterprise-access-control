using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EnterpriseAccessControl.Infrastructure.Auditing;

/// <summary>
/// Estampa automáticamente los metadatos de auditoría en cada escritura (Constitución, Principio
/// III; RF-026, RF-027, CS-005).
/// </summary>
/// <remarks>
/// Se implementa como interceptor de EF Core — y no como responsabilidad de cada caso de uso —
/// para que sea imposible olvidarlo: ninguna ruta de escritura puede sortearlo. Los valores vienen
/// siempre del servidor (<see cref="IUsuarioActualAccessor"/> + <see cref="IRelojSistema"/>), nunca
/// del payload del cliente.
///
/// La revocación automática en cascada (RF-061 a RF-065) reutiliza este mismo mecanismo: al
/// ejecutarse dentro de la transacción del cese, cada fila revocada queda estampada con el usuario
/// que registró el cese y el instante en que ocurrió, sin necesitar una entidad de log adicional
/// (research.md §14.2).
/// </remarks>
public sealed class AuditSaveChangesInterceptor(
    IUsuarioActualAccessor usuarioActual,
    IRelojSistema reloj) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Estampar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Estampar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Estampar(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var ahora = reloj.UtcNow;
        var usuarioId = usuarioActual.UsuarioId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = ahora;
                    entry.Entity.CreatedById = usuarioId;
                    entry.Entity.UpdatedAt = ahora;
                    entry.Entity.UpdatedById = usuarioId;
                    break;

                case EntityState.Modified:
                    // CreatedAt/CreatedById nunca se reescriben: se marcan explícitamente como no
                    // modificados por si el cliente intentó enviarlos en el payload.
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedById)).IsModified = false;
                    entry.Entity.UpdatedAt = ahora;
                    entry.Entity.UpdatedById = usuarioId;
                    break;

                default:
                    break;
            }
        }
    }
}
