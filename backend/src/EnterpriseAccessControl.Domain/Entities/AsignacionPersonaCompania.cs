using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Pertenencia temporal de una persona a una compañía (RF-014, RF-047).
/// </summary>
/// <remarks>
/// **Nota de secuenciación**: esta entidad pertenece conceptualmente a la Historia 5, que implementa
/// su ciclo de vida completo —estado, motivo de fin, renovación, cascada de revocación y trigger de
/// no-solapamiento (T102)—. Se crea aquí, en la Historia 4, únicamente porque la búsqueda de personas
/// debe limitarse al alcance de compañías del usuario (RF-035) y esa restricción no puede evaluarse
/// sin saber a qué compañía pertenece cada persona. Es el mismo patrón ya aplicado a <c>Compañía</c>
/// (movida a Foundational) y a <c>AsignaciónCredencial</c> (movida a US5): se adelanta la entidad, no
/// la capacidad de negocio.
///
/// Representa la relación **laboral o contractual**, y su compañía puede ser indistintamente
/// PRINCIPAL_MANDANTE o CONTRATISTA (RF-047). **No** indica para qué Compañía Principal trabaja o
/// accede la persona: eso lo modela <c>ContextoOperativoPersonaPrincipal</c>, del que una persona
/// puede tener varios vigentes a la vez (RF-052, CS-013).
///
/// <see cref="FechaHoraFin"/> es obligatoria desde la creación: RF-071 no admite vigencia indefinida
/// —ni con <c>null</c> ni con fecha centinela— en ninguna asociación temporal vinculada a una
/// persona.
/// </remarks>
public class AsignacionPersonaCompania : EntidadBase
{
    public required Guid PersonaId { get; set; }

    /// <summary>Compañía de pertenencia; puede ser PRINCIPAL_MANDANTE o CONTRATISTA (RF-047).</summary>
    public required Guid CompaniaId { get; set; }

    public required DateTime FechaHoraInicio { get; set; }

    /// <summary>
    /// Fin de vigencia, obligatorio desde la creación (RF-071).
    /// </summary>
    /// <remarks>
    /// Un cierre —por cese o por reemplazo— solo puede **acortarla**; la renovación (RF-073) es la
    /// única operación que la extiende, y no cierra la pertenencia.
    /// </remarks>
    public required DateTime FechaHoraFin { get; set; }

    /// <summary>
    /// Estado administrativo (research.md §14.2). Nunca sustituye a la comparación de fechas.
    /// </summary>
    public EstadoPertenencia Estado { get; set; } = EstadoPertenencia.ACTIVA;

    /// <summary>Causa del cierre; poblado solo cuando <see cref="Estado"/> es FINALIZADA.</summary>
    public MotivoFinPertenencia? MotivoFin { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Indica si la pertenencia puede renovarse en el instante indicado (RF-073).
    /// </summary>
    /// <remarks>
    /// Exige las dos condiciones a la vez: estado ACTIVA **y** vigencia dinámica. Una pertenencia
    /// que sigue ACTIVA pero cuya fecha de fin ya pasó no es renovable —renovarla puentearía
    /// retroactivamente un vacío temporal ya transcurrido—; requiere una pertenencia nueva.
    /// </remarks>
    public bool EsRenovableEn(DateTime instante) =>
        Estado == EstadoPertenencia.ACTIVA && instante <= FechaHoraFin;

    /// <summary>Indica si la pertenencia está vigente en el instante indicado.</summary>
    /// <remarks>
    /// La vigencia se evalúa siempre contra las fechas y nunca a partir de un campo de estado: el
    /// mero paso del tiempo no modifica filas (Principio IV, RF-063). El <c>Estado</c> administrativo
    /// lo añade la Historia 5 (T102) y tampoco sustituirá a esta comparación.
    /// </remarks>
    public bool EstaVigenteEn(DateTime instante) =>
        FechaHoraInicio <= instante && instante < FechaHoraFin;
}
