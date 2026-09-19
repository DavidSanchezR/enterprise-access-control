using FluentValidation;

namespace EnterpriseAccessControl.Application.Permissions.Validators;

/// <summary>
/// Validación de borde de un permiso de acceso (RF-021, RF-071, contracts/permissions.yaml).
/// </summary>
/// <remarks>
/// Mismo motivo que los validadores de la Historia 5: omitir <c>fechaHoraFinVigencia</c> en el JSON
/// la enlaza a <c>default(DateTime)</c> y el error aflora después como un período inválido. El
/// problema real es un campo obligatorio ausente, así que se rechaza aquí con 400 y un mensaje que
/// lo dice.
///
/// Solo se comprueba presencia. Orden de fechas, bloques horarios y exclusividad del sujeto son
/// reglas de negocio y viven en <see cref="PermisoAccesoService"/>.
/// </remarks>
public sealed class PermisoAccesoRequestValidator : AbstractValidator<PermisoAccesoRequest>
{
    public PermisoAccesoRequestValidator()
    {
        RuleFor(r => r.AreaAccesoId)
            .NotEqual(Guid.Empty).WithMessage("El área de acceso es obligatoria.");

        RuleFor(r => r.FechaHoraInicioVigencia)
            .NotEqual(default(DateTime))
            .WithName("fechaHoraInicioVigencia")
            .WithMessage("La fecha de inicio de vigencia es obligatoria.");

        RuleFor(r => r.FechaHoraFinVigencia)
            .NotEqual(default(DateTime))
            .WithName("fechaHoraFinVigencia")
            .WithMessage(
                "La fecha de fin de vigencia es obligatoria para los tres alcances: no existe vigencia indefinida (RF-021, RF-071).");

        RuleFor(r => r.BloquesHorarios)
            .NotNull().WithMessage("Los bloques horarios son obligatorios (RF-022).");
    }
}

/// <summary>Validación de borde de una evaluación de acceso (contracts/access-evaluation.yaml).</summary>
/// <remarks>
/// Sin ella, omitir <c>fechaHora</c> evaluaría el año 1 y devolvería un DENEGADO con apariencia de
/// resultado legítimo, cuando lo que hay es una petición incompleta.
/// </remarks>
public sealed class EvaluarAccesoRequestValidator : AbstractValidator<EvaluarAccesoRequest>
{
    public EvaluarAccesoRequestValidator()
    {
        RuleFor(r => r.PersonaId).NotEqual(Guid.Empty).WithMessage("La persona es obligatoria.");
        RuleFor(r => r.AreaAccesoId).NotEqual(Guid.Empty).WithMessage("El área es obligatoria.");

        RuleFor(r => r.FechaHora)
            .NotEqual(default(DateTime))
            .WithName("fechaHora")
            .WithMessage("La fecha/hora a evaluar es obligatoria.");
    }
}
