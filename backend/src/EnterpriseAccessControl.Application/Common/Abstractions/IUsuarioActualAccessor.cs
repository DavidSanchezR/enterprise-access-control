namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Expone la identidad del usuario autenticado de la request en curso (research.md §3, §6).
/// </summary>
/// <remarks>
/// Es un puerto: la Aplicación no conoce <c>HttpContext</c> ni <c>ClaimsPrincipal</c>. El adaptador
/// de Infraestructura resuelve el valor desde los claims del JWT. Los campos de auditoría se
/// estampan con este valor y NUNCA con datos enviados por el cliente (Principio III).
/// </remarks>
public interface IUsuarioActualAccessor
{
    /// <summary>Id del usuario autenticado, o <c>null</c> en operaciones sin usuario (p. ej. migraciones o seed).</summary>
    Guid? UsuarioId { get; }
}
