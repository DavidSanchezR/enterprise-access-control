using System.ComponentModel.DataAnnotations;

namespace EnterpriseAccessControl.Application.Common.Options;

/// <summary>
/// Configuración de emisión y validación del JWT (research.md §2, Options Pattern).
/// Validada con ValidateOnStart() para fallar al arranque, no en el primer login.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>Clave de firma simétrica; debe provenir de variable de entorno o secret store, nunca del repositorio.</summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "La clave de firma JWT debe tener al menos 32 caracteres.")]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutos { get; init; } = 60;
}
