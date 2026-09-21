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

        RuleFor(r => r.Rol).IsInEnum().WithMessage("El rol administrativo no es válido.");

        RuleFor(r => r).CumpleReglaFundamental();

        RuleFor(r => r.FechaHoraFin)
            .GreaterThan(r => r.FechaHoraInicio)
            .WithMessage("La fecha de fin debe ser posterior a la de inicio.");
    }
}

/// <summary>
/// Regla fundamental de <c>CompañíaId</c> frente al rol, reutilizable por los dos requests que
/// crean una asignación (RF-074).
/// </summary>
/// <remarks>
/// Se valida también en el borde HTTP —además de en el dominio y en un <c>CHECK</c> de base de
/// datos— para que el cliente reciba un 400 con el campo concreto en lugar de un error genérico.
/// </remarks>
public static class ReglasAsignacionRol
{
    public static IRuleBuilderOptions<T, T> CumpleReglaFundamental<T>(this IRuleBuilder<T, T> regla)
        where T : IAsignacionDeRol
    {
        ArgumentNullException.ThrowIfNull(regla);

        return regla
            .Must(r => r.Rol switch
            {
                Domain.Enums.RolAdministrativo.GLOBAL_ADMINISTRATOR => r.CompaniaId is null,
                Domain.Enums.RolAdministrativo.COMPANY_ADMINISTRATOR =>
                    r.CompaniaId is not null && r.CompaniaId != Guid.Empty,
                _ => false,
            })
            .WithMessage(
                "GLOBAL_ADMINISTRATOR no admite compañía; COMPANY_ADMINISTRATOR exige una compañía válida.");
    }
}

/// <summary>Valida una nueva asignación de rol (contracts/users.yaml, 400).</summary>
public sealed class AsignarRolRequestValidator : AbstractValidator<AsignarRolRequest>
{
    public AsignarRolRequestValidator()
    {
        RuleFor(r => r.Rol).IsInEnum().WithMessage("El rol administrativo no es válido.");

        RuleFor(r => r).CumpleReglaFundamental();

        RuleFor(r => r.FechaHoraFin)
            .GreaterThan(r => r.FechaHoraInicio)
            .WithMessage("La fecha de fin debe ser posterior a la de inicio.");
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

/// <summary>Valida la renovación de una asignación de rol (contracts/users.yaml, 400).</summary>
public sealed class RenovarAsignacionRolRequestValidator
    : AbstractValidator<RenovarAsignacionRolRequest>
{
    public RenovarAsignacionRolRequestValidator() =>
        RuleFor(r => r.FechaHoraFin)
            .NotEmpty().WithMessage("Debe indicarse la nueva fecha de fin.");
}
