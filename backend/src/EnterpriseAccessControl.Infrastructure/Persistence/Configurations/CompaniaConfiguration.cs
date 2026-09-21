using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

public sealed class CompaniaConfiguration : IEntityTypeConfiguration<Compania>
{
    public void Configure(EntityTypeBuilder<Compania> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Compania");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(200);

        builder.Property(c => c.NumeroDocumento).IsRequired().HasMaxLength(20);

        builder.Property(c => c.TipoCompania).IsRequired();

        builder.Property(c => c.Estado).IsRequired();

        // Anulable en la columna porque solo una PRINCIPAL_MANDANTE la exige (RF-080); la
        // obligatoriedad condicional la impone la capa de aplicación, que conoce el tipo.
        builder.Property(c => c.ZonaHorariaIana).HasMaxLength(64);

        builder.Property(c => c.RowVersion).ComoRowVersion();

        // Una compañía se identifica de forma única por su documento tributario/legal, no por nombre.
        builder
            .HasIndex(c => new { c.TipoDocumentoId, c.NumeroDocumento })
            .IsUnique()
            .HasDatabaseName("UX_Compania_TipoDocumento_NumeroDocumento");

        // El filtrado por tipo es la consulta más frecuente del dominio (selectores de Principal
        // vs. Contratista en casi todas las pantallas), de ahí el índice dedicado.
        builder.HasIndex(c => c.TipoCompania).HasDatabaseName("IX_Compania_TipoCompania");
    }
}
