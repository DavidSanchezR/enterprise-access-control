using FluentValidation;

namespace EnterpriseAccessControl.Application.Permissions.Validators;

/// <summary>
/// Validación de borde de un permiso de acceso (RF-021, RF-071, contracts/permissions.yaml).
/// </summary>
/// <remarks>
/// Mismo motivo que los validadores de la Historia 5: omitir <c>fechaFinVigencia</c> en el JSON
/// la enlaza a <c>default(DateOnly)</c> y el error aflora después como un período inválido. El
/// problema real es un campo obligatorio ausente, así que se rechaza aquí con 400 y un mensaje que
/// lo dice.
///
/// Desde v2.0.0 (VF-004) es también lo que rechaza a un cliente del contrato anterior: un cuerpo con
/// <c>fechaHoraInicioVigencia</c>/<c>fechaHoraFinVigencia</c> deja las fechas civiles sin informar, y el 400
/// nombra los campos nuevos en lugar de interpretar un instante.
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

        RuleFor(r => r.FechaInicioVigencia)
            .NotEqual(default(DateOnly))
            .WithName("fechaInicioVigencia")
            .WithMessage("La fecha de inicio de vigencia es obligatoria.");

        RuleFor(r => r.FechaFinVigencia)
            .NotEqual(default(DateOnly))
            .WithName("fechaFinVigencia")
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
