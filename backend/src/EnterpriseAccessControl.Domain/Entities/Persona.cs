using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Persona cuyo acceso físico administra el sistema (Historia 4, RF-012, RF-013, RF-041).
/// </summary>
/// <remarks>
/// Es una entidad distinta de <see cref="Usuario"/>: el usuario opera el sistema, la persona es el
/// sujeto cuyo acceso se evalúa. Una misma persona física puede ser ambas cosas, pero como dos
/// registros independientes.
///
/// El <c>Id</c> heredado es un UUID v7 autogenerado e inmutable que **no se muestra en las
/// interfaces gráficas normales** (RF-013): la persona se identifica ante el usuario por su
/// documento, no por un identificador técnico.
///
/// La identidad de negocio es el par <c>(TipoDocumentoId, NumeroDocumento)</c>, único en todo el
/// sistema (RF-041): dos registros con el mismo documento serían la misma persona duplicada, con
/// históricos de acceso partidos entre ambos.
/// </remarks>
public class Persona : EntidadBase
{
    public required string Nombres { get; set; }

    public required string Apellidos { get; set; }

    /// <summary>
    /// Fecha de nacimiento, sin componente horario: es un dato civil, no un instante.
    /// </summary>
    public required DateOnly FechaNacimiento { get; set; }

    public required Guid TipoDocumentoId { get; set; }

    public required string NumeroDocumento { get; set; }

    public required Guid GeneroId { get; set; }

    public required string CorreoElectronico { get; set; }

    public required Guid TipoSangreId { get; set; }

    /// <summary>Nombre de la persona a contactar ante una emergencia.</summary>
    public required string ContactoEmergencia { get; set; }

    /// <summary>Teléfono del contacto de emergencia.</summary>
    public required string NumeroEmergencia { get; set; }

    /// <summary>Token de concurrencia optimista (research.md §16).</summary>
    public byte[]? RowVersion { get; set; }
}
