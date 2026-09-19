using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Hashes de contraseñas anteriores de un usuario, para impedir su reutilización (RF-003,
/// research.md §2).
/// </summary>
/// <remarks>
/// Se guarda el hash, nunca la contraseña: la comprobación de reutilización se hace verificando la
/// contraseña candidata contra cada hash histórico, no comparando textos.
/// </remarks>
public class HistorialContrasena : EntidadBase
{
    public required Guid UsuarioId { get; set; }

    public required string PasswordHash { get; set; }
}
