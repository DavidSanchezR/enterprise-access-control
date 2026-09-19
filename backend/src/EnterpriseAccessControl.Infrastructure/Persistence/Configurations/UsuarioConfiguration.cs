using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Usuario");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Correo).IsRequired().HasMaxLength(256);
        builder.Property(u => u.CorreoNormalizado).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.Estado).IsRequired();
        builder.Property(u => u.RequiereCambioPassword).IsRequired();
        builder.Property(u => u.IntentosFallidosConsecutivos).IsRequired();
        builder.Property(u => u.FechaUltimoCambioPassword).IsRequired();
        builder.Property(u => u.RowVersion).ComoRowVersion();

        // El correo es el identificador de login: debe ser único en todo el sistema (RF-001).
        // La unicidad se impone sobre la forma normalizada para que "A@x.cl" y "a@x.cl" no puedan
        // coexistir aunque la intercalación de la base de datos fuese sensible a mayúsculas.
        builder
            .HasIndex(u => u.CorreoNormalizado)
            .IsUnique()
            .HasDatabaseName("UX_Usuario_CorreoNormalizado");
    }
}

public sealed class HistorialContrasenaConfiguration : IEntityTypeConfiguration<HistorialContrasena>
{
    public void Configure(EntityTypeBuilder<HistorialContrasena> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("HistorialContrasena");

        builder.Property(h => h.UsuarioId).IsRequired();
        builder.Property(h => h.PasswordHash).IsRequired().HasMaxLength(512);

        builder
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tabla de histórico de alto volumen de inserción: PK no-clúster + clúster por CreatedAt
        // para evitar fragmentación con PK GUID (research.md §20).
        builder.ConClusterPorCreatedAt();

        // La comprobación de reutilización consulta los N hashes más recientes del usuario.
        builder
            .HasIndex(h => new { h.UsuarioId, h.CreatedAt })
            .HasDatabaseName("IX_HistorialContrasena_Usuario_CreatedAt");
    }
}

public sealed class AlcanceUsuarioCompaniaConfiguration : IEntityTypeConfiguration<AlcanceUsuarioCompania>
{
    public void Configure(EntityTypeBuilder<AlcanceUsuarioCompania> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AlcanceUsuarioCompania");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UsuarioId).IsRequired();
        builder.Property(a => a.CompaniaId).IsRequired();

        builder
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(a => a.CompaniaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una compañía aparece a lo sumo una vez en el alcance de un usuario.
        builder
            .HasIndex(a => new { a.UsuarioId, a.CompaniaId })
            .IsUnique()
            .HasDatabaseName("UX_AlcanceUsuarioCompania_Usuario_Compania");
    }
}
