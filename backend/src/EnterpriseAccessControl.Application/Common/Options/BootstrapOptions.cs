using System.ComponentModel.DataAnnotations;

namespace EnterpriseAccessControl.Application.Common.Options;

/// <summary>
/// Identidad del primer administrador global, creado en el arranque (RF-078).
/// </summary>
/// <remarks>
/// **Ningún valor por defecto versionado, y menos aún la contraseña.** Un valor de respaldo en
/// <c>appsettings.json</c> sería una credencial conocida en cualquier despliegue que olvidara
/// sobrescribirla. Por eso ambos campos son obligatorios y el arranque falla si faltan: es preferible
/// que el despliegue no levante a que levante con un administrador adivinable.
///
/// En entornos sin gestor de secretos, la contraseña se suministra como variable de entorno
/// <c>Bootstrap__AdminPassword</c> (doble guion bajo: es la convención de ASP.NET Core para anidar
/// secciones). La contraseña debe cumplir la política vigente sin excepción y el usuario creado queda
/// obligado a cambiarla en su primer inicio de sesión.
/// </remarks>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>Correo del administrador inicial; es su identificador de login (RF-001).</summary>
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string AdminEmail { get; init; } = string.Empty;

    /// <summary>Contraseña semilla. Nunca se versiona; se inyecta por secreto o entorno.</summary>
    [Required(AllowEmptyStrings = false)]
    public string AdminPassword { get; init; } = string.Empty;
}
