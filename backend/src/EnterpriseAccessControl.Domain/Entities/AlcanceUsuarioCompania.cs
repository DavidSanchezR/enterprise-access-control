using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Compañía que un usuario puede administrar (RF-004).
/// </summary>
/// <remarks>
/// Entidad independiente —y no una columna en <c>Usuario</c>— porque un usuario administra N
/// compañías. Este alcance es puramente administrativo: NO deriva de, ni implica, ninguna
/// asignación operacional de personas (RF-050).
/// </remarks>
public class AlcanceUsuarioCompania : EntidadBase
{
    public required Guid UsuarioId { get; set; }

    public required Guid CompaniaId { get; set; }
}
