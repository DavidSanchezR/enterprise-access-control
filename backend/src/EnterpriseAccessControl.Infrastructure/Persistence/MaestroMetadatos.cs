using EnterpriseAccessControl.Application.Masters;
using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Infrastructure.Persistence;

/// <summary>
/// Resuelve las reglas de cada catálogo leyéndolas del modelo de EF Core (research.md §17).
/// </summary>
/// <remarks>
/// Se consulta el modelo en lugar de mantener una tabla paralela de longitudes y unicidades en la
/// capa de Aplicación: la configuración de persistencia ya es la fuente de verdad de esos valores, y
/// duplicarlos crearía dos sitios que pueden divergir sin que ninguna prueba lo note —justo el tipo
/// de desfase que produce un error de truncamiento en producción.
/// </remarks>
public sealed class MaestroMetadatos(AppDbContext db) : IMaestroMetadatos
{
    public int LongitudMaximaNombre<TEntity>()
        where TEntity : EntidadMaestra
    {
        var propiedad = db.Model
            .FindEntityType(typeof(TEntity))?
            .FindProperty(nameof(EntidadMaestra.Nombre));

        return propiedad?.GetMaxLength()
            ?? throw new InvalidOperationException(
                $"El catálogo {typeof(TEntity).Name} no declara longitud máxima para Nombre.");
    }

    public bool NombreEsUnico<TEntity>()
        where TEntity : EntidadMaestra =>
        db.Model
            .FindEntityType(typeof(TEntity))?
            .GetIndexes()
            .Any(indice =>
                indice.IsUnique &&
                indice.Properties.Count == 1 &&
                indice.Properties[0].Name == nameof(EntidadMaestra.Nombre))
        ?? false;
}
