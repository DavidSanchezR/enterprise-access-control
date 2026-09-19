using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Franja horaria de un día de la semana dentro de un permiso (Historia 8, RF-022).
/// </summary>
/// <remarks>
/// Las horas se expresan en hora local de la zona empresarial (<c>America/Lima</c>), no en UTC: un
/// bloque de negocio es "lunes de 08:00 a 17:00" y debe seguir significando eso aunque cambie el
/// desfase horario. La evaluación convierte el instante UTC a hora local antes de comparar
/// (research.md §5, §7).
///
/// La comparación de fin es exclusiva: un bloque 08:00–17:00 cubre hasta las 16:59:59 y no concede
/// acceso a las 17:00 en punto. Así dos bloques consecutivos del mismo día —08:00–12:00 y
/// 12:00–17:00— no se solapan y pueden coexistir.
/// </remarks>
public class BloqueHorarioPermiso : EntidadBase
{
    public required Guid PermisoAccesoId { get; set; }

    public required DiaSemana DiaSemana { get; set; }

    public required TimeOnly HoraInicio { get; set; }

    /// <summary>Fin del bloque; debe ser posterior a <see cref="HoraInicio"/> (RF-039).</summary>
    public required TimeOnly HoraFin { get; set; }

    /// <summary>Indica si el bloque cubre el día y la hora local indicados.</summary>
    public bool Cubre(DiaSemana dia, TimeOnly horaLocal) =>
        DiaSemana == dia && HoraInicio <= horaLocal && horaLocal < HoraFin;

    /// <summary>Indica si dos bloques del mismo día se pisan entre sí (RF-039).</summary>
    public bool SeSolapaCon(BloqueHorarioPermiso otro)
    {
        ArgumentNullException.ThrowIfNull(otro);

        return DiaSemana == otro.DiaSemana
            && HoraInicio < otro.HoraFin
            && otro.HoraInicio < HoraFin;
    }
}
