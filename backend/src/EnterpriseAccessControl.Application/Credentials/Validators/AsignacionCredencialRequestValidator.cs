using FluentValidation;

namespace EnterpriseAccessControl.Application.Credentials.Validators;

/// <summary>
/// Validación de borde del alta de credencial (RF-056, RF-071, contracts/credentials.yaml).
/// </summary>
/// <remarks>
/// Mismo motivo que los validadores de la Historia 5: un campo ausente en el JSON se enlaza a
/// <c>default</c> y el error aparecería después disfrazado de período inválido. Solo se comprueba
/// presencia; las reglas de negocio viven en <see cref="CredencialService"/>.
/// </remarks>
public sealed class AsignacionCredencialRequestValidator : AbstractValidator<AsignacionCredencialRequest>
{
    public AsignacionCredencialRequestValidator()
    {
        RuleFor(r => r.CompaniaPrincipalId)
            .NotEqual(Guid.Empty).WithMessage("La compañía principal es obligatoria.");

        RuleFor(r => r.TipoCredencialId)
            .NotEqual(Guid.Empty).WithMessage("El tipo de credencial es obligatorio.");

        RuleFor(r => r.FechaHoraInicio)
            .NotEqual(default(DateTime))
            .WithMessage("La fecha de inicio de vigencia es obligatoria.");

        RuleFor(r => r.FechaHoraFin)
            .NotEqual(default(DateTime))
            .WithMessage("La fecha de fin de vigencia es obligatoria: no existe credencial de vigencia indefinida (RF-071).");
    }
}
