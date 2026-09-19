using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

/// <summary>Nombres de los triggers de no-solapamiento, compartidos con sus migraciones.</summary>
internal static class Triggers
{
    public const string AsignacionPersonaCompania = "trg_AsignacionPersonaCompania_NoSolapamiento";
    public const string ContextoOperativo = "trg_ContextoOperativoPersonaPrincipal_NoSolapamiento";
    public const string AsignacionUnidadOrganizativa =
        "trg_AsignacionPersonaUnidadOrganizativa_NoSolapamiento";
    public const string AsignacionCredencial = "trg_AsignacionCredencial_NoSolapamiento";
}

public sealed class ContextoOperativoPersonaPrincipalConfiguration
    : IEntityTypeConfiguration<ContextoOperativoPersonaPrincipal>
{
    public void Configure(EntityTypeBuilder<ContextoOperativoPersonaPrincipal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Declarar el trigger es obligatorio: con rowversion, EF emitiría la escritura con cláusula
        // OUTPUT y SQL Server la rechaza (error 334) en tablas con triggers habilitados.
        builder.ToTable(
            "ContextoOperativoPersonaPrincipal",
            t => t.HasTrigger(Triggers.ContextoOperativo));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PersonaId).IsRequired();
        builder.Property(c => c.CompaniaPrincipalId).IsRequired();
        builder.Property(c => c.FechaHoraInicio).IsRequired();
        builder.Property(c => c.FechaHoraFin).IsRequired();
        builder.Property(c => c.Estado).IsRequired();
        builder.Property(c => c.RowVersion).ComoRowVersion();

        builder.HasOne<Persona>().WithMany()
            .HasForeignKey(c => c.PersonaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Compania>().WithMany()
            .HasForeignKey(c => c.CompaniaPrincipalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AsignacionPersonaCompania>().WithMany()
            .HasForeignKey(c => c.RevocadoPorPertenenciaId).OnDelete(DeleteBehavior.Restrict);

        // Alineado con la partición del trigger y con la consulta "contextos vigentes de esta persona".
        builder
            .HasIndex(c => new { c.PersonaId, c.CompaniaPrincipalId, c.FechaHoraInicio })
            .HasDatabaseName("IX_ContextoOperativo_Persona_Principal_Inicio");

        // La auditoría "qué revocó esta pertenencia" consulta por esta columna.
        builder
            .HasIndex(c => c.RevocadoPorPertenenciaId)
            .HasDatabaseName("IX_ContextoOperativo_RevocadoPorPertenenciaId");
    }
}

public sealed class AsignacionPersonaUnidadOrganizativaConfiguration
    : IEntityTypeConfiguration<AsignacionPersonaUnidadOrganizativa>
{
    public void Configure(EntityTypeBuilder<AsignacionPersonaUnidadOrganizativa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "AsignacionPersonaUnidadOrganizativa",
            t => t.HasTrigger(Triggers.AsignacionUnidadOrganizativa));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PersonaId).IsRequired();
        builder.Property(a => a.ContextoOperativoId).IsRequired();
        builder.Property(a => a.UnidadOrganizativaId).IsRequired();
        builder.Property(a => a.FechaHoraInicio).IsRequired();
        builder.Property(a => a.FechaHoraFin).IsRequired();
        builder.Property(a => a.Estado).IsRequired();
        builder.Property(a => a.RowVersion).ComoRowVersion();

        // PersonaId está denormalizado: se deduce del contexto, pero tenerlo aquí evita un join en
        // cada consulta de "asignaciones de esta persona", que es la más frecuente.
        builder.HasOne<Persona>().WithMany()
            .HasForeignKey(a => a.PersonaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContextoOperativoPersonaPrincipal>().WithMany()
            .HasForeignKey(a => a.ContextoOperativoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnidadOrganizativa>().WithMany()
            .HasForeignKey(a => a.UnidadOrganizativaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AsignacionPersonaCompania>().WithMany()
            .HasForeignKey(a => a.RevocadoPorPertenenciaId).OnDelete(DeleteBehavior.Restrict);

        // Partición del trigger: la exclusividad es por contexto, no por persona (RF-055, CS-014).
        builder
            .HasIndex(a => new { a.ContextoOperativoId, a.FechaHoraInicio })
            .HasDatabaseName("IX_AsignacionUO_Contexto_Inicio");

        builder
            .HasIndex(a => a.PersonaId)
            .HasDatabaseName("IX_AsignacionUO_PersonaId");
    }
}

public sealed class AsignacionCredencialConfiguration : IEntityTypeConfiguration<AsignacionCredencial>
{
    public void Configure(EntityTypeBuilder<AsignacionCredencial> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "AsignacionCredencial",
            t => t.HasTrigger(Triggers.AsignacionCredencial));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PersonaId).IsRequired();
        builder.Property(a => a.CompaniaPrincipalId).IsRequired();
        builder.Property(a => a.TipoCredencialId).IsRequired();
        builder.Property(a => a.FechaHoraInicio).IsRequired();
        builder.Property(a => a.FechaHoraFin).IsRequired();
        builder.Property(a => a.Estado).IsRequired();
        builder.Property(a => a.RowVersion).ComoRowVersion();

        builder.HasOne<Persona>().WithMany()
            .HasForeignKey(a => a.PersonaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Compania>().WithMany()
            .HasForeignKey(a => a.CompaniaPrincipalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TipoCredencial>().WithMany()
            .HasForeignKey(a => a.TipoCredencialId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AsignacionPersonaCompania>().WithMany()
            .HasForeignKey(a => a.RevocadoPorPertenenciaId).OnDelete(DeleteBehavior.Restrict);

        // Partición del trigger: como máximo una credencial ASIGNADO vigente por par
        // (persona, Principal) — pero sí simultáneas en Principales distintas (RF-057, CS-016).
        builder
            .HasIndex(a => new { a.PersonaId, a.CompaniaPrincipalId, a.Estado })
            .HasDatabaseName("IX_AsignacionCredencial_Persona_Principal_Estado");
    }
}

public sealed class AsignacionTipoPersonaConfiguration : IEntityTypeConfiguration<AsignacionTipoPersona>
{
    public void Configure(EntityTypeBuilder<AsignacionTipoPersona> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Sin trigger: RF-011 admite perfiles simultáneos, así que no hay exclusividad que imponer.
        builder.ToTable("AsignacionTipoPersona");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.PersonaId).IsRequired();
        builder.Property(a => a.TipoPersonaId).IsRequired();
        builder.Property(a => a.FechaHoraInicio).IsRequired();
        builder.Property(a => a.FechaHoraFin).IsRequired();
        builder.Property(a => a.Estado).IsRequired();

        builder.HasOne<Persona>().WithMany()
            .HasForeignKey(a => a.PersonaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TipoPersona>().WithMany()
            .HasForeignKey(a => a.TipoPersonaId).OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(a => new { a.PersonaId, a.FechaHoraInicio })
            .HasDatabaseName("IX_AsignacionTipoPersona_Persona_Inicio");
    }
}
