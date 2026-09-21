using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Asignación temporal de un rol administrativo a un usuario (RF-074, RF-075).
/// </summary>
/// <remarks>
/// Reemplaza a <c>AlcanceUsuarioCompañía</c>, que era un join plano Usuario×Compañía sin rol ni
/// vigencia. Aquí el alcance administrativo es una entidad auditable con vigencia propia, del mismo
/// modo que las asociaciones temporales de <c>Persona</c> — con una diferencia deliberada: esta
/// cuelga de <c>Usuario</c>, no de <c>Persona</c>, y la autorización administrativa sigue siendo
/// independiente de la pertenencia empresarial de una persona (RF-050).
///
/// **Regla fundamental** (RF-074): <c>Rol = GLOBAL_ADMINISTRATOR</c> ⇒ <c>CompaniaId = NULL</c>;
/// <c>Rol = COMPANY_ADMINISTRATOR</c> ⇒ <c>CompaniaId</c> obligatoria. Se impone en tres niveles:
/// este invariante de dominio, el servicio de aplicación y un <c>CHECK</c> en base de datos.
/// </remarks>
public class AsignacionRolAdministrativo : EntidadBase
{
    public required Guid UsuarioId { get; set; }

    public required RolAdministrativo Rol { get; set; }

    /// <summary>Compañía administrada; NULL si y solo si el rol es GLOBAL_ADMINISTRATOR (RF-074).</summary>
    public Guid? CompaniaId { get; set; }

    public required DateTime FechaHoraInicio { get; set; }

    /// <summary>
    /// Fin de vigencia, obligatorio desde la creación (RF-075, mismo patrón que RF-071).
    /// </summary>
    /// <remarks>
    /// Nunca NULL ni fecha centinela. La única excepción del sistema es la asignación sembrada por
    /// el arranque, que usa <c>MAX_VALIDITY_DATE</c> (RF-078) y no se generaliza a ninguna otra fila.
    /// </remarks>
    public required DateTime FechaHoraFin { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Indica si la asignación está vigente en el instante indicado.</summary>
    /// <remarks>
    /// La vigencia se evalúa siempre contra las fechas y nunca contra un campo de estado: el mero
    /// paso del tiempo no modifica filas (Principio IV).
    /// </remarks>
    public bool EstaVigenteEn(DateTime instante) =>
        FechaHoraInicio <= instante && instante < FechaHoraFin;

    /// <summary>
    /// Indica si la asignación puede renovarse en el instante indicado (RF-073, RF-075).
    /// </summary>
    /// <remarks>
    /// Una asignación cuya fecha de fin ya pasó no es renovable: extenderla puentearía
    /// retroactivamente un vacío de autorización ya transcurrido. Requiere una asignación nueva.
    /// </remarks>
    public bool EsRenovableEn(DateTime instante) => instante <= FechaHoraFin;

    /// <summary>Verifica la regla fundamental de <c>CompaniaId</c> frente al rol (RF-074).</summary>
    public bool CumpleReglaFundamental() => Rol switch
    {
        RolAdministrativo.GLOBAL_ADMINISTRATOR => CompaniaId is null,
        RolAdministrativo.COMPANY_ADMINISTRATOR => CompaniaId is not null,
        _ => false,
    };
}
