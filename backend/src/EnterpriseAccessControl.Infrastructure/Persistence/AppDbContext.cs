using System.Reflection;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistencia sobre SQL Server (plan.md, Technical Context; research.md §15).
/// </summary>
/// <remarks>
/// Las entidades se van registrando en sus respectivas historias de usuario mediante clases
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> descubiertas automáticamente desde este ensamblado, de
/// modo que agregar una entidad no obligue a tocar este archivo (menos conflictos entre historias
/// que avanzan en paralelo).
///
/// El estampado de auditoría NO vive aquí sino en <c>AuditSaveChangesInterceptor</c>, registrado en
/// la composición de dependencias.
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<HistorialContrasena> HistorialContrasenas => Set<HistorialContrasena>();

    public DbSet<AsignacionRolAdministrativo> AsignacionesRolAdministrativo =>
        Set<AsignacionRolAdministrativo>();

    public DbSet<Compania> Companias => Set<Compania>();

    public DbSet<UnidadOrganizativa> UnidadesOrganizativas => Set<UnidadOrganizativa>();

    public DbSet<CompaniaPrincipalUnidadOrganizativaRaiz> RaicesUnidadOrganizativa =>
        Set<CompaniaPrincipalUnidadOrganizativaRaiz>();

    public DbSet<RelacionContratistaPrincipal> RelacionesContratistaPrincipal =>
        Set<RelacionContratistaPrincipal>();

    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<AsignacionPersonaCompania> AsignacionesPersonaCompania =>
        Set<AsignacionPersonaCompania>();

    public DbSet<ContextoOperativoPersonaPrincipal> ContextosOperativos =>
        Set<ContextoOperativoPersonaPrincipal>();

    public DbSet<AsignacionPersonaUnidadOrganizativa> AsignacionesUnidadOrganizativa =>
        Set<AsignacionPersonaUnidadOrganizativa>();

    public DbSet<AsignacionCredencial> AsignacionesCredencial => Set<AsignacionCredencial>();

    public DbSet<AsignacionTipoPersona> AsignacionesTipoPersona => Set<AsignacionTipoPersona>();

    public DbSet<AreaAcceso> AreasAcceso => Set<AreaAcceso>();

    public DbSet<AreaAccesoTipoPersona> AreasAccesoTipoPersona => Set<AreaAccesoTipoPersona>();

    public DbSet<PermisoAcceso> PermisosAcceso => Set<PermisoAcceso>();

    public DbSet<BloqueHorarioPermiso> BloquesHorarioPermiso => Set<BloqueHorarioPermiso>();

    public DbSet<TEntity> Maestro<TEntity>() where TEntity : EntidadMaestra => Set<TEntity>();

    public async Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        // Si ya hay una transacción en curso (por ejemplo, un caso de uso que compone a otro), se
        // reutiliza en lugar de anidar: SQL Server no admite transacciones anidadas reales.
        if (Database.CurrentTransaction is not null)
        {
            await operacion(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var transaccion = await Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await operacion(cancellationToken).ConfigureAwait(false);

        await transaccion.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ModelConventions.AplicarConvencionesGlobales(modelBuilder);
    }
}
