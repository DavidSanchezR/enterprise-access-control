using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAccessControl.Infrastructure.Persistence.Configurations;

public sealed class UnidadOrganizativaConfiguration : IEntityTypeConfiguration<UnidadOrganizativa>
{
    public void Configure(EntityTypeBuilder<UnidadOrganizativa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UnidadOrganizativa");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Estado).IsRequired();
        builder.Property(u => u.RowVersion).ComoRowVersion();

        // Autorreferencia padre-hijo. Restrict y no Cascade: borrar un nodo intermedio no debe
        // arrastrar en silencio su subárbol completo — esa decisión es del caso de uso, no del motor.
        builder
            .HasOne<UnidadOrganizativa>()
            .WithMany()
            .HasForeignKey(u => u.UnidadSuperiorId)
            .OnDelete(DeleteBehavior.Restrict);

        // El recorrido del árbol (listar hijos de un nodo, construir el árbol anidado) se hace
        // siempre por el padre.
        builder
            .HasIndex(u => u.UnidadSuperiorId)
            .HasDatabaseName("IX_UnidadOrganizativa_UnidadSuperiorId");

        // NOTA (RF-044): esta entidad no declara ninguna propiedad ni FK hacia Compañía, y no debe
        // declararla. La prueba de aislamiento multi-Principal verifica esta ausencia sobre el modelo
        // de EF Core para que una adición accidental falle de inmediato.
    }
}

public sealed class CompaniaPrincipalUnidadOrganizativaRaizConfiguration
    : IEntityTypeConfiguration<CompaniaPrincipalUnidadOrganizativaRaiz>
{
    public void Configure(EntityTypeBuilder<CompaniaPrincipalUnidadOrganizativaRaiz> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CompaniaPrincipalUnidadOrganizativaRaiz");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CompaniaId).IsRequired();
        builder.Property(e => e.UnidadOrganizativaRaizId).IsRequired();

        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(e => e.CompaniaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<UnidadOrganizativa>()
            .WithMany()
            .HasForeignKey(e => e.UnidadOrganizativaRaizId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un árbol pertenece a una sola Compañía Principal (RF-043): la unicidad va sobre la raíz,
        // no sobre el par, para que la base de datos impida por sí sola que dos Principales
        // reclamen el mismo árbol.
        builder
            .HasIndex(e => e.UnidadOrganizativaRaizId)
            .IsUnique()
            .HasDatabaseName("UX_CompaniaPrincipalUnidadOrganizativaRaiz_Raiz");

        // Resolver "los árboles de esta Principal" es la consulta de entrada de toda la Historia 2.
        builder
            .HasIndex(e => e.CompaniaId)
            .HasDatabaseName("IX_CompaniaPrincipalUnidadOrganizativaRaiz_CompaniaId");
    }
}

public sealed class RelacionContratistaPrincipalConfiguration
    : IEntityTypeConfiguration<RelacionContratistaPrincipal>
{
    public void Configure(EntityTypeBuilder<RelacionContratistaPrincipal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Declarar el trigger es obligatorio, no informativo: para recuperar el rowversion generado,
        // EF Core emite la escritura con una cláusula OUTPUT, y SQL Server la rechaza (error 334) en
        // tablas con triggers habilitados. Al conocer su existencia, EF cambia a la estrategia
        // compatible (SELECT posterior) en lugar de fallar en tiempo de ejecución.
        builder.ToTable(
            "RelacionContratistaPrincipal",
            t => t.HasTrigger("trg_RelacionContratistaPrincipal_NoSolapamiento"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CompaniaContratistaId).IsRequired();
        builder.Property(r => r.CompaniaPrincipalId).IsRequired();
        builder.Property(r => r.FechaHoraInicio).IsRequired();

        // Anulable a propósito: null = vigencia abierta. RF-071 (fecha de fin obligatoria) aplica
        // únicamente a las asociaciones temporales vinculadas a una Persona, y ésta une dos
        // compañías.
        builder.Property(r => r.FechaHoraFin);

        builder.Property(r => r.RowVersion).ComoRowVersion();

        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(r => r.CompaniaContratistaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Compania>()
            .WithMany()
            .HasForeignKey(r => r.CompaniaPrincipalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice alineado con la partición del trigger de no-solapamiento (research.md §5) y con la
        // consulta "relaciones de esta Contratista".
        builder
            .HasIndex(r => new { r.CompaniaContratistaId, r.CompaniaPrincipalId, r.FechaHoraInicio })
            .HasDatabaseName("IX_RelacionContratistaPrincipal_Par_Inicio");
    }
}
