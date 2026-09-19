using System.ComponentModel.DataAnnotations;

namespace EnterpriseAccessControl.Application.Common.Options;

/// <summary>
/// Umbrales de política de contraseñas (research.md §2). Son decisión de trabajo pendiente de
/// confirmación de negocio (spec.md, Decisiones Pendientes #1) — por eso viven en configuración y
/// no hardcodeados, para ajustarse sin desplegar código.
/// </summary>
public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";

    [Range(8, 128)]
    public int LongitudMinima { get; init; } = 10;

    public bool RequiereMayuscula { get; init; } = true;

    public bool RequiereMinuscula { get; init; } = true;

    public bool RequiereDigito { get; init; } = true;

    /// <summary>Intentos fallidos consecutivos antes de pasar el usuario a BLOQUEADO (RF-002).</summary>
    [Range(1, 20)]
    public int IntentosFallidosParaBloqueo { get; init; } = 5;

    /// <summary>Días de vigencia de la contraseña antes de forzar cambio (RF-003, Historia 1 criterio 3).</summary>
    [Range(1, 3650)]
    public int DiasExpiracion { get; init; } = 90;

    /// <summary>Cantidad de contraseñas anteriores que no pueden reutilizarse (HistorialContraseña, RF-003).</summary>
    [Range(1, 50)]
    public int HistorialNoReutilizable { get; init; } = 5;
}
