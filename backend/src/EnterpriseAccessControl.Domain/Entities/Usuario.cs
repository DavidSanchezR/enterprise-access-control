using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Usuario administrativo de la aplicación (RF-001 a RF-003).
/// </summary>
/// <remarks>
/// Es una entidad distinta de <c>Persona</c>: el usuario opera el sistema, la persona es el sujeto
/// cuyo acceso físico se evalúa. Su alcance administrativo (<c>AlcanceUsuarioCompañía</c>) es
/// independiente de cualquier relación operacional Persona→Compañía→UnidadOrganizativa (RF-050).
/// </remarks>
public class Usuario : EntidadBase
{
    private string _correo = string.Empty;

    /// <summary>Identificador de login. Se conserva tal como lo escribió el administrador.</summary>
    public required string Correo
    {
        get => _correo;
        set
        {
            _correo = value;
            CorreoNormalizado = NormalizarCorreo(value);
        }
    }

    /// <summary>
    /// Forma canónica del correo usada para búsqueda y unicidad.
    /// </summary>
    /// <remarks>
    /// Se persiste en lugar de aplicar <c>UPPER()</c> en la consulta: así la comparación es un
    /// predicado sargable sobre el índice único y no depende de la intercalación de la base de datos
    /// ni de la cultura del proceso.
    /// </remarks>
    public string CorreoNormalizado { get; private set; } = string.Empty;

    /// <summary>Regla única de normalización del correo, compartida por consultas y escrituras.</summary>
    public static string NormalizarCorreo(string? correo) =>
        (correo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Hash PBKDF2 producido por <c>PasswordHasher&lt;T&gt;</c>; nunca la contraseña en claro (RF-003).</summary>
    public required string PasswordHash { get; set; }

    public EstadoUsuario Estado { get; set; } = EstadoUsuario.ACTIVO;

    /// <summary>Fuerza el cambio en el siguiente login exitoso (Historia 1, criterio 3).</summary>
    public bool RequiereCambioPassword { get; set; }

    /// <summary>Se reinicia a cero tras un login exitoso; al alcanzar el umbral pasa a BLOQUEADO (RF-002).</summary>
    public int IntentosFallidosConsecutivos { get; set; }

    /// <summary>Base para calcular la expiración periódica de la contraseña (RF-003).</summary>
    public DateTime FechaUltimoCambioPassword { get; set; }

    public byte[]? RowVersion { get; set; }
}
