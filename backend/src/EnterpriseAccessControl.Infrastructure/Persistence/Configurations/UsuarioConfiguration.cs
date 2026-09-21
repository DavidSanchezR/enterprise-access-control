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

public sealed class AsignacionRolAdministrativoConfiguration
    : IEntityTypeConfiguration<AsignacionRolAdministrativo>
{
    /// <summary>
    /// Nombre del <c>CHECK</c> que impone la regla fundamental de <c>CompaniaId</c> (RF-074).
    /// </summary>
    /// <remarks>
    /// Se expone para que la migración y las pruebas se refieran a la misma restricción sin
    /// duplicar el literal.
    /// </remarks>
    public const string CheckReglaFundamental = "CK_AsignacionRolAdministrativo_RolCompania";

    public void Configure(EntityTypeBuilder<AsignacionRolAdministrativo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Declarar el trigger es obligatorio: con rowversion, EF emitiría la escritura con cláusula
        // OUTPUT y SQL Server la rechaza (error 334) en tablas con triggers habilitados.
        builder.ToTable(
            "AsignacionRolAdministrativo",
            t =>
            {
                t.HasTrigger(Triggers.AsignacionRolAdministrativo);

                t.HasCheckConstraint(
                    CheckReglaFundamental,
                    "([Rol] = 'GLOBAL_ADMINISTRATOR' AND [CompaniaId] IS NULL) "
                    + "OR ([Rol] = 'COMPANY_ADMINISTRATOR' AND [CompaniaId] IS NOT NULL)");
            });

        builder.Property(a => a.UsuarioId).IsRequired();
        builder.Property(a => a.Rol).IsRequired();

        // Anulable a propósito: es NULL exactamente para GLOBAL_ADMINISTRATOR (RF-074).
        builder.Property(a => a.CompaniaId);

        builder.Property(a => a.FechaHoraInicio).IsRequired();
        builder.Property(a => a.FechaHoraFin).IsRequired();
        builder.Property(a => a.RowVersion).ComoRowVersion();

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

        // Entidad de histórico: nunca se borra una asignación vencida, se acumulan (research.md §20).
        builder.ConClusterPorCreatedAt();

        // La resolución del alcance en cada request consulta las asignaciones vigentes del usuario.
        builder
            .HasIndex(a => new { a.UsuarioId, a.FechaHoraFin })
            .HasDatabaseName("IX_AsignacionRolAdministrativo_Usuario_FechaHoraFin");

        // El listado por compañía (Resource Ownership de usuarios) filtra por CompaniaId.
        builder
            .HasIndex(a => new { a.CompaniaId, a.FechaHoraFin })
            .HasDatabaseName("IX_AsignacionRolAdministrativo_Compania_FechaHoraFin");
    }
}
