using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Raíz común de las asociaciones que dependen de una pertenencia y son revocables en cascada
/// (RF-061 a RF-065, research.md §14.2).
/// </summary>
/// <remarks>
/// Agrupa <see cref="ContextoOperativoPersonaPrincipal"/> y
/// <see cref="AsignacionPersonaUnidadOrganizativa"/>, que comparten exactamente los mismos campos de
/// cierre. <see cref="AsignacionCredencial"/> queda fuera a propósito: su estado tiene cuatro valores
/// propios y el valor <c>REVOCADA</c> ya cumple la función de <see cref="MotivoFin"/> sin duplicar
/// información (research.md §14.2).
/// </remarks>
public abstract class AsociacionRevocable : EntidadBase
{
    public required Guid PersonaId { get; set; }

    public required DateTime FechaHoraInicio { get; set; }

    /// <summary>Fin de vigencia, obligatorio desde la creación (RF-071).</summary>
    public required DateTime FechaHoraFin { get; set; }

    /// <summary>Estado administrativo; no determina por sí solo la autorización (RF-063).</summary>
    public Estado Estado { get; set; } = Estado.ACTIVO;

    /// <summary>Causa del cierre; poblado solo cuando <see cref="Estado"/> es INACTIVO.</summary>
    public MotivoFinRevocacion? MotivoFin { get; set; }

    /// <summary>
    /// Pertenencia que originó la revocación en cascada; poblado solo cuando
    /// <see cref="MotivoFin"/> es <c>REVOCACION_CESE_PERTENENCIA</c>.
    /// </summary>
    /// <remarks>
    /// Responde directamente la pregunta de auditoría "qué quedó revocado por esta pertenencia", sin
    /// heurísticas de fecha o compañía (research.md §14.2).
    /// </remarks>
    public Guid? RevocadoPorPertenenciaId { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Vigencia efectiva: siempre por fechas, nunca por <see cref="Estado"/> (Principio IV).</summary>
    public bool EstaVigenteEn(DateTime instante) =>
        FechaHoraInicio <= instante && instante < FechaHoraFin;
}

/// <summary>
/// Relación operativa entre una persona y una Compañía Principal (RF-052 a RF-054).
/// </summary>
/// <remarks>
/// Es lo que responde "¿para qué Principal trabaja o accede esta persona?", una pregunta distinta de
/// "¿qué compañía la contrata?" —esa la responde <see cref="AsignacionPersonaCompania"/>—. Una
/// persona puede tener **varios contextos vigentes a la vez**, uno por Principal, sin límite superior
/// (RF-052, CS-030): un trabajador de una contratista puede prestar servicios simultáneos a dos
/// mineras.
///
/// Todos los contextos abiertos de una persona dependen necesariamente de su única pertenencia activa
/// (RF-014), y por eso la cascada los alcanza a todos cuando esa pertenencia se cierra
/// (research.md §14.4).
/// </remarks>
public class ContextoOperativoPersonaPrincipal : AsociacionRevocable
{
    /// <summary>Compañía PRINCIPAL_MANDANTE del contexto (RF-053, RF-054).</summary>
    public required Guid CompaniaPrincipalId { get; set; }
}

/// <summary>
/// Unidad organizativa en la que la persona presta servicios dentro de un contexto operativo
/// (RF-015, RF-055).
/// </summary>
/// <remarks>
/// La exclusividad es **por contexto**, no por persona: la misma persona puede tener una unidad
/// vigente en cada uno de sus contextos simultáneos (CS-014). Por eso el trigger de no-solapamiento
/// se particiona por <see cref="ContextoOperativoId"/> y no por <c>PersonaId</c> (research.md §5).
/// </remarks>
public class AsignacionPersonaUnidadOrganizativa : AsociacionRevocable
{
    /// <summary>Contexto operativo al que pertenece la asignación; determina su Compañía Principal.</summary>
    public required Guid ContextoOperativoId { get; set; }

    public required Guid UnidadOrganizativaId { get; set; }
}

/// <summary>
/// Credencial o fotocheck asignado a una persona en el contexto de una Compañía Principal
/// (RF-018, RF-056, RF-057).
/// </summary>
/// <remarks>
/// No hereda de <see cref="AsociacionRevocable"/> porque su estado tiene semántica propia: el valor
/// <c>REVOCADA</c> ya expresa la causa del cierre y añadir un <c>MotivoFin</c> duplicaría el dato
/// (research.md §14.2).
///
/// Una credencial vigente es condición **necesaria pero no suficiente** para conceder acceso: su
/// ausencia deniega por defecto (RF-066, paso 6 del algoritmo de evaluación).
/// </remarks>
public class AsignacionCredencial : EntidadBase
{
    public required Guid PersonaId { get; set; }

    /// <summary>Compañía PRINCIPAL_MANDANTE en cuyo contexto se emite la credencial (RF-056).</summary>
    public required Guid CompaniaPrincipalId { get; set; }

    /// <summary>Tipo o diseño visual de la credencial; nunca una tecnología física (RF-058).</summary>
    public required Guid TipoCredencialId { get; set; }

    public required DateTime FechaHoraInicio { get; set; }

    /// <summary>Fin de vigencia, obligatorio desde la creación (RF-071).</summary>
    public required DateTime FechaHoraFin { get; set; }

    public EstadoCredencial Estado { get; set; } = EstadoCredencial.ASIGNADO;

    /// <summary>Pertenencia que originó la revocación; poblado solo si <see cref="Estado"/> es REVOCADA.</summary>
    public Guid? RevocadoPorPertenenciaId { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Vigencia efectiva para autorización (RF-070): requiere estado ASIGNADO y estar dentro de la
    /// ventana temporal.
    /// </summary>
    /// <remarks>
    /// Una fila ASIGNADO cuya fecha de fin ya pasó está temporalmente expirada sin que su estado
    /// cambie: el mero vencimiento no dispara ninguna transición.
    ///
    /// Ambos extremos son inclusivos, tal como lo enuncian RF-070, research.md §7 paso 6 y §24 y
    /// contracts/access-evaluation.yaml (<c>FechaHoraInicio &lt;= fecha evaluada &lt;= FechaHoraFin</c>).
    /// Con el fin normalizado a 23:59:59.999, el último milisegundo del día sigue cubierto.
    /// </remarks>
    public bool EstaVigenteEn(DateTime instante) =>
        Estado == EstadoCredencial.ASIGNADO
        && FechaHoraInicio <= instante
        && instante <= FechaHoraFin;
}

/// <summary>
/// Perfil vigente de una persona, por ejemplo Trabajador o Visitante (RF-011).
/// </summary>
/// <remarks>
/// A diferencia de compañía y unidad organizativa, **admite varios perfiles simultáneos** sin
/// exclusividad mutua (RF-011).
///
/// Tampoco está sujeta a la contención temporal de RF-072: el modelo de dominio no la declara
/// dependiente de la pertenencia, y asumirlo sería inventar una dependencia que nadie estableció.
/// Sí le aplica RF-071: su fecha de fin es obligatoria.
/// </remarks>
public class AsignacionTipoPersona : EntidadBase
{
    public required Guid PersonaId { get; set; }

    public required Guid TipoPersonaId { get; set; }

    public required DateTime FechaHoraInicio { get; set; }

    /// <summary>Fin de vigencia, obligatorio desde la creación (RF-071).</summary>
    public required DateTime FechaHoraFin { get; set; }

    public Estado Estado { get; set; } = Estado.ACTIVO;

    public bool EstaVigenteEn(DateTime instante) =>
        FechaHoraInicio <= instante && instante < FechaHoraFin;
}
