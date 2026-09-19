using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

public sealed class AreaAccesoConfiguration : IEntityTypeConfiguration<AreaAcceso>
{
    public void Configure(EntityTypeBuilder<AreaAcceso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AreaAcceso");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(a => a.CompaniaPrincipalId).IsRequired();
        builder.Property(a => a.Estado).IsRequired();
        builder.Property(a => a.RowVersion).ComoRowVersion();

        // Autorreferencia padre-hijo. Restrict y no Cascade: borrar un nodo intermedio no debe
        // arrastrar en silencio su subárbol — esa decisión es del caso de uso, no del motor.
        builder
            .HasOne<AreaAcceso>()
            .WithMany()
            .HasForeignKey(a => a.AreaSuperiorId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK directa hacia Compañía, a diferencia de UnidadOrganizativa (RF-046 frente a RF-044).
        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(a => a.CompaniaPrincipalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Consulta de entrada de toda la historia: "el árbol de áreas de esta Principal".
        builder
            .HasIndex(a => new { a.CompaniaPrincipalId, a.AreaSuperiorId })
            .HasDatabaseName("IX_AreaAcceso_Principal_AreaSuperior");

        builder
            .HasIndex(a => a.AreaSuperiorId)
            .HasDatabaseName("IX_AreaAcceso_AreaSuperiorId");
    }
}

public sealed class AreaAccesoTipoPersonaConfiguration : IEntityTypeConfiguration<AreaAccesoTipoPersona>
{
    public void Configure(EntityTypeBuilder<AreaAccesoTipoPersona> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AreaAccesoTipoPersona");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AreaAccesoId).IsRequired();
        builder.Property(a => a.TipoPersonaId).IsRequired();

        // Cascade hacia el área: la autorización no significa nada sin el área que la concede, y
        // dejarla huérfana convertiría una fila de basura en un permiso aparente.
        builder
            .HasOne<AreaAcceso>()
            .WithMany()
            .HasForeignKey(a => a.AreaAccesoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict hacia el catálogo: un TipoPersona en uso no debe poder borrarse; se inactiva
        // (RF-032), y el histórico que ya lo referencia permanece.
        builder
            .HasOne<TipoPersona>()
            .WithMany()
            .HasForeignKey(a => a.TipoPersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un tipo de persona aparece a lo sumo una vez por área (data-model.md §ÁreaAccesoTipoPersona).
        builder
            .HasIndex(a => new { a.AreaAccesoId, a.TipoPersonaId })
            .IsUnique()
            .HasDatabaseName("UX_AreaAccesoTipoPersona_Area_TipoPersona");
    }
}
