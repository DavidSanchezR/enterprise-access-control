using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Acceso a datos para los casos de uso de Aplicación.
/// </summary>
/// <remarks>
/// **Por qué esta forma y no un repositorio genérico**: research.md §15 fija LINQ como mecanismo
/// normal de consulta y descarta añadir un repositorio genérico "solo por abstracción". Exponer los
/// <c>DbSet</c> permite escribir consultas LINQ directas —incluidas las proyecciones y filtros por
/// alcance que exige el Principio I— sin interponer una capa que solo reenviaría llamadas.
///
/// La restricción de research.md §15 aplica al <b>Dominio</b>, que sigue sin conocer EF Core ni SQL
/// Server; la Aplicación sí puede depender de EF Core.
/// </remarks>
public interface IAppDbContext
{
    DbSet<Usuario> Usuarios { get; }

    DbSet<HistorialContrasena> HistorialContrasenas { get; }

    DbSet<AlcanceUsuarioCompania> AlcancesUsuarioCompania { get; }

    DbSet<Compania> Companias { get; }

    DbSet<UnidadOrganizativa> UnidadesOrganizativas { get; }

    DbSet<CompaniaPrincipalUnidadOrganizativaRaiz> RaicesUnidadOrganizativa { get; }

    DbSet<RelacionContratistaPrincipal> RelacionesContratistaPrincipal { get; }

    DbSet<Persona> Personas { get; }

    DbSet<AsignacionPersonaCompania> AsignacionesPersonaCompania { get; }

    DbSet<ContextoOperativoPersonaPrincipal> ContextosOperativos { get; }

    DbSet<AsignacionPersonaUnidadOrganizativa> AsignacionesUnidadOrganizativa { get; }

    DbSet<AsignacionCredencial> AsignacionesCredencial { get; }

    DbSet<AsignacionTipoPersona> AsignacionesTipoPersona { get; }

    DbSet<AreaAcceso> AreasAcceso { get; }

    DbSet<AreaAccesoTipoPersona> AreasAccesoTipoPersona { get; }

    DbSet<PermisoAcceso> PermisosAcceso { get; }

    DbSet<BloqueHorarioPermiso> BloquesHorarioPermiso { get; }

    /// <summary>
    /// Conjunto de un catálogo maestro cualquiera (Historia 3).
    /// </summary>
    /// <remarks>
    /// Se expone de forma genérica en lugar de con cinco propiedades porque los catálogos los
    /// mantiene un único servicio genérico; declararlos uno a uno obligaría a tocar este puerto cada
    /// vez que se añada un catálogo, sin ganar nada a cambio.
    /// </remarks>
    DbSet<TEntity> Maestro<TEntity>() where TEntity : EntidadMaestra;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta varias escrituras como una sola unidad atómica.
    /// </summary>
    /// <remarks>
    /// Necesario cuando un caso de uso debe emitir sus sentencias en un orden concreto y no puede
    /// resolverse con un único <c>SaveChanges</c>. El ejemplo del dominio es declarar una relación
    /// Contratista↔Principal: hay que cerrar la vigente <em>antes</em> de insertar la nueva, porque
    /// el trigger de no-solapamiento se evalúa por sentencia y vería las dos abiertas si el orden se
    /// invirtiera. EF Core no garantiza el orden relativo entre un UPDATE y un INSERT independientes
    /// dentro del mismo lote, así que el orden debe imponerlo el caso de uso.
    /// </remarks>
    Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion,
        CancellationToken cancellationToken = default);
}
