using EnterpriseAccessControl.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>Resultado de evaluar una contraseña candidata contra la política vigente.</summary>
public sealed record ResultadoPolitica(bool EsValida, IReadOnlyList<string> Errores)
{
    public static ResultadoPolitica Valida { get; } = new(true, []);
}

/// <summary>
/// Valida la complejidad de una contraseña según los umbrales configurados (RF-003, research.md §2).
/// </summary>
/// <remarks>
/// Los umbrales vienen de <see cref="PasswordPolicyOptions"/> y no están incrustados en el código
/// porque siguen siendo una decisión de trabajo pendiente de confirmación de negocio (spec.md,
/// Decisiones Pendientes #1): deben poder ajustarse sin desplegar.
///
/// La comprobación de <em>reutilización</em> no vive aquí sino en el servicio de autenticación,
/// porque requiere acceso al historial de hashes del usuario.
/// </remarks>
public sealed class PasswordPolicyValidator(IOptions<PasswordPolicyOptions> opciones)
{
    private readonly PasswordPolicyOptions _politica = opciones.Value;

    public ResultadoPolitica Validar(string? password)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errores.Add("La contraseña es obligatoria.");
            return new ResultadoPolitica(false, errores);
        }

        if (password.Length < _politica.LongitudMinima)
        {
            errores.Add($"La contraseña debe tener al menos {_politica.LongitudMinima} caracteres.");
        }

        if (_politica.RequiereMayuscula && !password.Any(char.IsUpper))
        {
            errores.Add("La contraseña debe incluir al menos una letra mayúscula.");
        }

        if (_politica.RequiereMinuscula && !password.Any(char.IsLower))
        {
            errores.Add("La contraseña debe incluir al menos una letra minúscula.");
        }

        if (_politica.RequiereDigito && !password.Any(char.IsDigit))
        {
            errores.Add("La contraseña debe incluir al menos un dígito.");
        }

        return errores.Count == 0 ? ResultadoPolitica.Valida : new ResultadoPolitica(false, errores);
    }
}
