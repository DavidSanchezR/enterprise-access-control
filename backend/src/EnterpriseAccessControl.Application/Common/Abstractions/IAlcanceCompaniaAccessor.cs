using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Alcance administrativo efectivo del usuario autenticado (RF-074, RF-077).
/// </summary>
/// <remarks>
/// Segunda línea de defensa del Principio I: el <c>CompaniaScopeAuthorizationHandler</c> actúa como
/// gate de primera línea a nivel de endpoint, y este accessor alimenta el filtro de consulta de la
/// capa de datos. Un usuario nunca debe leer, listar ni modificar datos de una compañía fuera de su
/// alcance, incluso conociendo el identificador del recurso.
///
/// Desde la Sesión 2026-09-20 (D1) el alcance deja de ser una lista plana de compañías y pasa a
/// derivarse del rol administrativo vigente: <c>GLOBAL_ADMINISTRATOR</c> alcanza **todas** las
/// compañías sin enumerar ninguna, y <c>COMPANY_ADMINISTRATOR</c> queda limitado a las compañías de
/// sus asignaciones vigentes. Por eso <see cref="EstaEnAlcance"/> es la pregunta correcta y
/// <see cref="CompaniaIds"/> no debe usarse como si fuera el alcance completo.
/// </remarks>
public interface IAlcanceCompaniaAccessor
{
    /// <summary>Rol vigente del usuario autenticado; <c>null</c> si no tiene ninguna asignación vigente.</summary>
    RolAdministrativo? Rol { get; }

    /// <summary>Indica si el alcance es GLOBAL, es decir, toda compañía sin enumerarlas (RF-074).</summary>
    bool EsGlobal { get; }

    /// <summary>
    /// Compañías administradas explícitamente por asignaciones <c>COMPANY_ADMINISTRATOR</c> vigentes.
    /// </summary>
    /// <remarks>
    /// Vacío para un <c>GLOBAL_ADMINISTRATOR</c>: su alcance no se enumera. Para filtrar una consulta
    /// por alcance hay que contemplar antes <see cref="EsGlobal"/>, no asumir que este conjunto lo
    /// describe todo.
    /// </remarks>
    IReadOnlySet<Guid> CompaniaIds { get; }

    /// <summary>Indica si el usuario tiene al menos una asignación de rol vigente (RF-077).</summary>
    bool TieneAlcanceVigente { get; }

    /// <summary>Indica si la compañía indicada está dentro del alcance del usuario autenticado.</summary>
    bool EstaEnAlcance(Guid companiaId);
}
