using EnterpriseAccessControl.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// Convenciones de persistencia específicas de SQL Server que cada entidad activa explícitamente.
/// </summary>
public static class ConfiguracionExtensions
{
    /// <summary>
    /// Aplica la estrategia de clustering para entidades de histórico de alto volumen de inserción
    /// (research.md §20): PK <c>Id</c> como NONCLUSTERED más un índice CLUSTERED sobre
    /// <c>CreatedAt</c>.
    /// </summary>
    /// <remarks>
    /// SQL Server ordena físicamente las filas por el índice clúster y, por defecto, ese índice es
    /// la PK. Insertar GUIDs como clave del índice clúster provoca fragmentación y page splits.
    /// UUID v7 mitiga esto solo parcialmente, porque SQL Server compara <c>uniqueidentifier</c> en
    /// un orden de bytes distinto al de generación. Clusterizar por <c>CreatedAt</c> —columna que
    /// ya existe por auditoría y crece monotónicamente— evita el problema justo en las tablas donde
    /// importa, sin añadir una columna técnica extra.
    ///
    /// Se aplica solo a las 6 entidades de histórico de alto volumen; el resto conserva el
    /// comportamiento por defecto.
    /// </remarks>
    public static EntityTypeBuilder<TEntity> ConClusterPorCreatedAt<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : EntidadBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(e => e.Id).IsClustered(false);

        builder
            .HasIndex(e => e.CreatedAt)
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_CreatedAt_Clustered")
            .IsClustered();

        return builder;
    }

    /// <summary>
    /// Habilita concurrencia optimista mediante <c>rowversion</c> de SQL Server (research.md §16).
    /// </summary>
    /// <remarks>
    /// Reservado a las entidades con escritura concurrente real. Un conflicto se traduce a
    /// <c>409 Conflict</c> con ProblemDetails en el middleware de excepciones, en lugar de
    /// propagar una excepción de infraestructura al cliente.
    /// </remarks>
    public static PropertyBuilder<byte[]?> ComoRowVersion(this PropertyBuilder<byte[]?> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // La propiedad se declara anulable en el dominio porque la base de datos genera el valor:
        // una entidad recién construida en memoria aún no lo tiene.
        return builder.IsRowVersion();
    }
}
