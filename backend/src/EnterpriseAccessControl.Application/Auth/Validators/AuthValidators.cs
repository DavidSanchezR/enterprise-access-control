using FluentValidation;

namespace EnterpriseAccessControl.Application.Auth.Validators;

/// <summary>
/// Regla de complejidad de contraseña reutilizable, delegada en <see cref="PasswordPolicyValidator"/>.
/// </summary>
/// <remarks>
/// La política no se reimplementa aquí con reglas de FluentValidation (<c>MinimumLength</c>,
/// <c>Matches</c>, …) a propósito: los umbrales son configurables (Decisiones Pendientes #1) y
/// duplicarlos crearía dos fuentes de verdad que podrían divergir. El servicio de aplicación vuelve a
/// aplicar la misma política antes de escribir, de modo que un consumidor que no pase por el pipeline
/// HTTP tampoco pueda saltarse la regla.
/// </remarks>
public static class ReglasPassword
{
    public static IRuleBuilderOptionsConditions<T, string> CumplePolitica<T>(
        this IRuleBuilder<T, string> regla,
        PasswordPolicyValidator politica)
    {
        ArgumentNullException.ThrowIfNull(regla);
        ArgumentNullException.ThrowIfNull(politica);

        return regla.Custom((valor, contexto) =>
        {
            var resultado = politica.Validar(valor);
            if (resultado.EsValida)
            {
                return;
            }

            foreach (var error in resultado.Errores)
            {
                contexto.AddFailure(error);
            }
        });
    }
}

/// <summary>Valida el alta de usuario (contracts/users.yaml, 400).</summary>
public sealed class CrearUsuarioRequestValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioRequestValidator(PasswordPolicyValidator politica)
    {
        RuleFor(r => r.Correo)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede exceder 256 caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

        RuleFor(r => r.PasswordInicial).CumplePolitica(politica);

        // contracts/users.yaml declara minItems: 1 — un usuario nuevo sin ninguna compañía en su
        // alcance no podría operar sobre nada, así que se rechaza el alta en lugar de crearlo inerte.
        // (En el reemplazo de alcance sí se admite vaciarlo: ahí es una acción deliberada.)
        RuleFor(r => r.CompaniaIds)
            .NotNull().WithMessage("Debe indicarse el alcance de compañías.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Debe indicarse al menos una compañía en el alcance del usuario.");

        // Un identificador vacío nunca puede corresponder a una compañía real: se rechaza en el borde
        // en lugar de llegar a la consulta de validación de compañías.
        RuleForEach(r => r.CompaniaIds)
            .NotEqual(Guid.Empty).WithMessage("El identificador de compañía no es válido.");
    }
}

/// <summary>Valida la actualización de usuario (contracts/users.yaml, 400).</summary>
public sealed class ActualizarUsuarioRequestValidator : AbstractValidator<ActualizarUsuarioRequest>
{
    public ActualizarUsuarioRequestValidator()
    {
        RuleFor(r => r.Correo)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede exceder 256 caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

        RuleFor(r => r.Estado)
            .IsInEnum().WithMessage("El estado del usuario no es válido.");
    }
}

/// <summary>Valida el cambio de contraseña (contracts/auth.yaml, 400).</summary>
public sealed class CambiarPasswordRequestValidator : AbstractValidator<CambiarPasswordRequest>
{
    public CambiarPasswordRequestValidator(PasswordPolicyValidator politica)
    {
        RuleFor(r => r.PasswordActual)
            .NotEmpty().WithMessage("Debe indicar su contraseña actual.");

        RuleFor(r => r.PasswordNueva).CumplePolitica(politica);

        // Comprobación barata en el borde; la regla completa de no reutilización (últimas N
        // contraseñas) vive en AutenticacionService porque necesita el historial de hashes.
        RuleFor(r => r.PasswordNueva)
            .NotEqual(r => r.PasswordActual)
            .WithMessage("La contraseña nueva debe ser distinta de la actual.");
    }
}

/// <summary>Valida el login (contracts/auth.yaml, 400).</summary>
/// <remarks>
/// Sólo comprueba presencia: aplicar aquí la política de complejidad revelaría, ante una contraseña
/// mal formada, que el fallo no es de credenciales, y facilitaría distinguir cuentas existentes.
/// </remarks>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Correo).NotEmpty().WithMessage("El correo es obligatorio.");
        RuleFor(r => r.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}

/// <summary>Valida el reemplazo del alcance de compañías (contracts/users.yaml, 400).</summary>
public sealed class ReemplazarAlcanceRequestValidator : AbstractValidator<ReemplazarAlcanceRequest>
{
    public ReemplazarAlcanceRequestValidator()
    {
        RuleFor(r => r.CompaniaIds)
            .NotNull().WithMessage("Debe indicarse el alcance de compañías, aunque sea vacío.");

        RuleForEach(r => r.CompaniaIds)
            .NotEqual(Guid.Empty).WithMessage("El identificador de compañía no es válido.");
    }
}
