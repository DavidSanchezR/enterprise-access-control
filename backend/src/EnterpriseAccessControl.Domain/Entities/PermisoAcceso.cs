using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Permiso de acceso a un área, otorgado a una persona, a una unidad organizativa o a una compañía
/// (Historia 8, RF-020, RF-021).
/// </summary>
/// <remarks>
/// Exactamente uno de <see cref="PersonaId"/>, <see cref="UnidadOrganizativaId"/> y
/// <see cref="CompaniaId"/> es no nulo, y debe corresponder con <see cref="Alcance"/>. La coherencia
/// la respalda un <c>CHECK</c> en la base de datos además de la validación de aplicación: un permiso
/// con alcance PERSONA y un <c>UnidadOrganizativaId</c> poblado sería inevaluable, y conviene que
/// sea imposible de escribir, no solo improbable.
///
/// Cuando <see cref="Alcance"/> es COMPANIA, <see cref="CompaniaId"/> admite cualquier
/// <c>TipoCompania</c> (research.md §12): dar acceso a todo el personal de una contratista sobre un
/// área de la Principal a la que presta servicios es un caso legítimo. El acceso efectivo sigue
/// condicionado a que cada persona tenga contexto operativo y credencial vigentes con esa Principal.
///
/// **No** está sujeto a la contención temporal de RF-072 ni a la revocación en cascada de RF-061: un
/// permiso es configuración del área, no una asociación de la persona con su compañía. Tratarlo como
/// dependiente de la pertenencia obligaría a reescribir permisos de alcance COMPAÑÍA cada vez que
/// alguien cambia de empresa, cuando lo correcto es que la evaluación deje de encontrarlos aplicables.
/// </remarks>
public class PermisoAcceso : EntidadBase
{
    public required Guid AreaAccesoId { get; set; }

    public required AlcancePermiso Alcance { get; set; }

    public Guid? PersonaId { get; set; }

    public Guid? UnidadOrganizativaId { get; set; }

    public Guid? CompaniaId { get; set; }

    public required DateTime FechaHoraInicioVigencia { get; set; }

    /// <summary>
    /// Fin de vigencia, obligatorio para los tres alcances (RF-021, RF-071).
    /// </summary>
    /// <remarks>
    /// No admite null como "vigencia abierta" ni fechas centinela: un permiso de acceso sin fin
    /// conocido sobrevive indefinidamente a la razón que lo motivó.
    /// </remarks>
    public required DateTime FechaHoraFinVigencia { get; set; }

    public Estado Estado { get; set; } = Estado.ACTIVO;

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Vigencia efectiva: siempre por fechas, nunca por <see cref="Estado"/> (Principio IV).</summary>
    public bool EstaVigenteEn(DateTime instante) =>
        FechaHoraInicioVigencia <= instante && instante < FechaHoraFinVigencia;

    /// <summary>Sujeto al que se otorga el permiso, según su alcance.</summary>
    public Guid? SujetoId => Alcance switch
    {
        AlcancePermiso.PERSONA => PersonaId,
        AlcancePermiso.UNIDAD_ORGANIZATIVA => UnidadOrganizativaId,
        AlcancePermiso.COMPANIA => CompaniaId,
        _ => null,
    };
}
