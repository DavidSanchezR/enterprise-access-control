using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

public sealed class PermisoAccesoConfiguration : IEntityTypeConfiguration<PermisoAcceso>
{
    /// <summary>
    /// Exactamente una de las tres referencias de sujeto es no nula, y coincide con el alcance.
    /// </summary>
    /// <remarks>
    /// La validación también vive en el servicio, pero aquí es donde queda garantizada: un permiso
    /// con alcance PERSONA y una unidad organizativa poblada sería inevaluable, y conviene que sea
    /// imposible de escribir —también por una migración o un script— y no solo improbable.
    ///
    /// Los valores del enum se comparan como texto porque las convenciones globales persisten todo
    /// enum como <c>nvarchar</c> (research.md §17).
    /// </remarks>
    internal const string CheckExclusividad = """
        (Alcance = 'PERSONA'
             AND PersonaId IS NOT NULL
             AND UnidadOrganizativaId IS NULL
             AND CompaniaId IS NULL)
        OR (Alcance = 'UNIDAD_ORGANIZATIVA'
             AND PersonaId IS NULL
             AND UnidadOrganizativaId IS NOT NULL
             AND CompaniaId IS NULL)
        OR (Alcance = 'COMPANIA'
             AND PersonaId IS NULL
             AND UnidadOrganizativaId IS NULL
             AND CompaniaId IS NOT NULL)
        """;

    public void Configure(EntityTypeBuilder<PermisoAcceso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "PermisoAcceso",
            t => t.HasCheckConstraint("CK_PermisoAcceso_SujetoSegunAlcance", CheckExclusividad));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AreaAccesoId).IsRequired();
        builder.Property(p => p.Alcance).IsRequired();
        builder.Property(p => p.FechaHoraInicioVigencia).IsRequired();

        // RF-021, RF-071: obligatoria para los tres alcances, sin null ni fecha centinela.
        builder.Property(p => p.FechaHoraFinVigencia).IsRequired();

        builder.Property(p => p.Estado).IsRequired();
        builder.Property(p => p.RowVersion).ComoRowVersion();

        // SujetoId es una propiedad calculada del dominio, no una columna.
        builder.Ignore(p => p.SujetoId);

        builder
            .HasOne<AreaAcceso>()
            .WithMany()
            .HasForeignKey(p => p.AreaAccesoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Persona>()
            .WithMany()
            .HasForeignKey(p => p.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<UnidadOrganizativa>()
            .WithMany()
            .HasForeignKey(p => p.UnidadOrganizativaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(p => p.CompaniaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Consulta de entrada del paso 10 del algoritmo: "los permisos de esta área".
        builder
            .HasIndex(p => new { p.AreaAccesoId, p.Alcance })
            .HasDatabaseName("IX_PermisoAcceso_Area_Alcance");

        // Índices filtrados por sujeto: el listado del contrato filtra por cada uno por separado, y
        // dos de las tres columnas son null en cualquier fila.
        builder
            .HasIndex(p => p.PersonaId)
            .HasFilter("PersonaId IS NOT NULL")
            .HasDatabaseName("IX_PermisoAcceso_PersonaId");

        builder
            .HasIndex(p => p.UnidadOrganizativaId)
            .HasFilter("UnidadOrganizativaId IS NOT NULL")
            .HasDatabaseName("IX_PermisoAcceso_UnidadOrganizativaId");

        builder
            .HasIndex(p => p.CompaniaId)
            .HasFilter("CompaniaId IS NOT NULL")
            .HasDatabaseName("IX_PermisoAcceso_CompaniaId");
    }
}

public sealed class BloqueHorarioPermisoConfiguration : IEntityTypeConfiguration<BloqueHorarioPermiso>
{
    public void Configure(EntityTypeBuilder<BloqueHorarioPermiso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BloqueHorarioPermiso");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.PermisoAccesoId).IsRequired();
        builder.Property(b => b.DiaSemana).IsRequired();

        // Hora local de la zona empresarial, sin fecha ni desfase: el bloque es una regla de
        // negocio ("lunes de 08:00 a 17:00"), no un instante (research.md §5).
        builder.Property(b => b.HoraInicio).IsRequired().HasColumnType("time(0)");
        builder.Property(b => b.HoraFin).IsRequired().HasColumnType("time(0)");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_BloqueHorarioPermiso_HoraFinPosterior",
            "HoraFin > HoraInicio"));

        // Cascade: los bloques no tienen sentido sin su permiso, y dejarlos huérfanos convertiría
        // filas muertas en un horario aparente.
        builder
            .HasOne<PermisoAcceso>()
            .WithMany()
            .HasForeignKey(b => b.PermisoAccesoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(b => new { b.PermisoAccesoId, b.DiaSemana })
            .HasDatabaseName("IX_BloqueHorarioPermiso_Permiso_Dia");
    }
}
