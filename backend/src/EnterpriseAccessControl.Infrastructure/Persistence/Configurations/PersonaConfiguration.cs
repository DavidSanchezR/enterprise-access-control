using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

public sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Persona");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombres).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Apellidos).IsRequired().HasMaxLength(150);
        builder.Property(p => p.NumeroDocumento).IsRequired().HasMaxLength(20);
        builder.Property(p => p.CorreoElectronico).IsRequired().HasMaxLength(256);
        builder.Property(p => p.ContactoEmergencia).IsRequired().HasMaxLength(150);
        builder.Property(p => p.NumeroEmergencia).IsRequired().HasMaxLength(30);

        // Dato civil sin hora: `date` evita arrastrar un componente horario que induciría a
        // comparaciones erróneas por zona horaria.
        builder.Property(p => p.FechaNacimiento).IsRequired().HasColumnType("date");

        builder.Property(p => p.RowVersion).ComoRowVersion();

        foreach (var maestro in new[]
                 {
                     nameof(Persona.TipoDocumentoId),
                     nameof(Persona.GeneroId),
                     nameof(Persona.TipoSangreId),
                 })
        {
            builder.Property<Guid>(maestro).IsRequired();
        }

        // Restrict y no Cascade: un valor de catálogo nunca debe poder arrastrar personas al
        // borrarse. El retiro de un valor es un cambio de estado a INACTIVO (RF-032), no un DELETE.
        builder
            .HasOne<TipoDocumento>()
            .WithMany()
            .HasForeignKey(p => p.TipoDocumentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Genero>()
            .WithMany()
            .HasForeignKey(p => p.GeneroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<TipoSangre>()
            .WithMany()
            .HasForeignKey(p => p.TipoSangreId)
            .OnDelete(DeleteBehavior.Restrict);

        // Identidad de negocio (RF-041): el mismo documento no puede pertenecer a dos registros, o
        // el histórico de acceso de una persona quedaría partido entre ambos.
        builder
            .HasIndex(p => new { p.TipoDocumentoId, p.NumeroDocumento })
            .IsUnique()
            .HasDatabaseName("UX_Persona_TipoDocumento_NumeroDocumento");

        // La búsqueda por apellidos y nombres es la consulta de entrada de todas las pantallas de
        // persona y debe sostener 100.000 registros con p95 < 2 s (CS-002).
        builder
            .HasIndex(p => new { p.Apellidos, p.Nombres })
            .HasDatabaseName("IX_Persona_Apellidos_Nombres");

        builder
            .HasIndex(p => p.NumeroDocumento)
            .HasDatabaseName("IX_Persona_NumeroDocumento");
    }
}

public sealed class AsignacionPersonaCompaniaConfiguration
    : IEntityTypeConfiguration<AsignacionPersonaCompania>
{
    public void Configure(EntityTypeBuilder<AsignacionPersonaCompania> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // El trigger de no-solapamiento (T102) obliga a declararlo: con rowversion, EF usaría una
        // cláusula OUTPUT que SQL Server rechaza en tablas con triggers (error 334).
        builder.ToTable(
            "AsignacionPersonaCompania",
            t => t.HasTrigger(Triggers.AsignacionPersonaCompania));
        builder.HasKey(a => a.Id);

        builder.Property(a => a.PersonaId).IsRequired();
        builder.Property(a => a.CompaniaId).IsRequired();
        builder.Property(a => a.FechaHoraInicio).IsRequired();

        // NOT NULL desde la creación: RF-071 no admite vigencia indefinida en las asociaciones
        // temporales de una persona, ni con null ni con fecha centinela.
        builder.Property(a => a.FechaHoraFin).IsRequired();
        builder.Property(a => a.Estado).IsRequired();

        builder.Property(a => a.RowVersion).ComoRowVersion();

        builder
            .HasOne<Persona>()
            .WithMany()
            .HasForeignKey(a => a.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(a => a.CompaniaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice alineado con la consulta que sostiene el filtro por alcance: "pertenencias de esta
        // persona vigentes en tal instante". Lo usará también el trigger de no-solapamiento
        // particionado por PersonaId que añade la Historia 5 (T102).
        builder
            .HasIndex(a => new { a.PersonaId, a.FechaHoraInicio, a.FechaHoraFin })
            .HasDatabaseName("IX_AsignacionPersonaCompania_Persona_Vigencia");

        builder
            .HasIndex(a => a.CompaniaId)
            .HasDatabaseName("IX_AsignacionPersonaCompania_CompaniaId");
    }
}
