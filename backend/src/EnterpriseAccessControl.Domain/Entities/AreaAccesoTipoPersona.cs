using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Tipo de persona autorizado a acceder a un área (Historia 7, RF-019).
/// </summary>
/// <remarks>
/// Declara qué perfiles —Trabajador, Visitante, Proveedor…— pueden acceder al área. Es una condición
/// del área, no un permiso: quién accede realmente lo decide <c>PermisoAcceso</c> (Historia 8), y
/// esta tabla acota el conjunto de perfiles sobre los que esa decisión tiene sentido.
///
/// A diferencia de las asociaciones de persona de la Historia 5, aquí no hay vigencia: un área
/// admite un tipo de persona o no lo admite, sin ventana temporal. Por eso la entidad no lleva
/// FechaHoraInicio/Fin y RF-071 no le aplica: no vincula a ninguna persona.
/// </remarks>
public class AreaAccesoTipoPersona : EntidadBase
{
    public required Guid AreaAccesoId { get; set; }

    public required Guid TipoPersonaId { get; set; }
}
