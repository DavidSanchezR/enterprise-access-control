using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración compartida por los cinco catálogos maestros (data-model.md).
/// </summary>
/// <remarks>
/// La longitud de <c>Nombre</c> y la unicidad se declaran por catálogo en lugar de imponer un valor
/// común: data-model.md fija límites distintos (100 para tipo de documento, persona y credencial; 50
/// para género; 10 para tipo de sangre) y marca "Único" en cuatro de los cinco.
/// </remarks>
internal static class MaestroConfiguracion
{
    public static void ConfigurarMaestro<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string tabla,
        int longitudNombre,
        bool nombreUnico)
        where TEntity : EntidadMaestra
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(tabla);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(longitudNombre);
        builder.Property(e => e.Estado).IsRequired();

        if (nombreUnico)
        {
            builder
                .HasIndex(e => e.Nombre)
                .IsUnique()
                .HasDatabaseName($"UX_{tabla}_Nombre");
        }
    }
}

public sealed class TipoDocumentoConfiguration : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> builder) =>
        builder.ConfigurarMaestro("TipoDocumento", longitudNombre: 100, nombreUnico: true);
}

public sealed class TipoSangreConfiguration : IEntityTypeConfiguration<TipoSangre>
{
    public void Configure(EntityTypeBuilder<TipoSangre> builder) =>
        builder.ConfigurarMaestro("TipoSangre", longitudNombre: 10, nombreUnico: true);
}

public sealed class GeneroConfiguration : IEntityTypeConfiguration<Genero>
{
    public void Configure(EntityTypeBuilder<Genero> builder) =>
        builder.ConfigurarMaestro("Genero", longitudNombre: 50, nombreUnico: true);
}

public sealed class TipoPersonaConfiguration : IEntityTypeConfiguration<TipoPersona>
{
    public void Configure(EntityTypeBuilder<TipoPersona> builder) =>
        builder.ConfigurarMaestro("TipoPersona", longitudNombre: 100, nombreUnico: true);
}

public sealed class TipoCredencialConfiguration : IEntityTypeConfiguration<TipoCredencial>
{
    /// <remarks>
    /// Único catálogo sin índice único sobre el nombre: data-model.md marca "Único" en los otros
    /// cuatro y no en éste. Se respeta la especificación en lugar de uniformar por cuenta propia —
    /// la asimetría queda reportada como hallazgo para que negocio confirme si es deliberada.
    /// </remarks>
    public void Configure(EntityTypeBuilder<TipoCredencial> builder) =>
        builder.ConfigurarMaestro("TipoCredencial", longitudNombre: 100, nombreUnico: false);
}
