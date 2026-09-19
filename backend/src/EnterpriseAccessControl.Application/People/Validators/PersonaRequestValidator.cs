using FluentValidation;

namespace EnterpriseAccessControl.Application.People.Validators;

/// <summary>
/// Validación de entrada del registro de personas (RF-012, contracts/people.yaml).
/// </summary>
/// <remarks>
/// Los diez campos son obligatorios: la especificación no admite un registro parcial, porque un
/// contacto de emergencia o un tipo de sangre ausentes son justamente los datos que hacen falta
/// cuando ocurre un accidente en faena.
///
/// Las longitudes replican las columnas declaradas en data-model.md, de modo que un valor demasiado
/// largo se rechaza con 400 y no llega a la base de datos como error de truncamiento.
/// </remarks>
public sealed class PersonaRequestValidator : AbstractValidator<PersonaRequest>
{
    public PersonaRequestValidator()
    {
        RuleFor(r => r.Nombres)
            .NotEmpty().WithMessage("Los nombres son obligatorios.")
            .MaximumLength(150).WithMessage("Los nombres no pueden exceder 150 caracteres.");

        RuleFor(r => r.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son obligatorios.")
            .MaximumLength(150).WithMessage("Los apellidos no pueden exceder 150 caracteres.");

        RuleFor(r => r.FechaNacimiento)
            .NotEqual(default(DateOnly)).WithMessage("La fecha de nacimiento es obligatoria.")
            .Must(fecha => fecha < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de nacimiento debe ser anterior a hoy.");

        RuleFor(r => r.TipoDocumentoId)
            .NotEqual(Guid.Empty).WithMessage("El tipo de documento es obligatorio.");

        RuleFor(r => r.NumeroDocumento)
            .NotEmpty().WithMessage("El número de documento es obligatorio.")
            .MaximumLength(20).WithMessage("El número de documento no puede exceder 20 caracteres.");

        RuleFor(r => r.GeneroId)
            .NotEqual(Guid.Empty).WithMessage("El género es obligatorio.");

        RuleFor(r => r.CorreoElectronico)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede exceder 256 caracteres.")
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.");

        RuleFor(r => r.TipoSangreId)
            .NotEqual(Guid.Empty).WithMessage("El tipo de sangre es obligatorio.");

        RuleFor(r => r.ContactoEmergencia)
            .NotEmpty().WithMessage("El contacto de emergencia es obligatorio.")
            .MaximumLength(150).WithMessage("El contacto de emergencia no puede exceder 150 caracteres.");

        RuleFor(r => r.NumeroEmergencia)
            .NotEmpty().WithMessage("El número de emergencia es obligatorio.")
            .MaximumLength(30).WithMessage("El número de emergencia no puede exceder 30 caracteres.");
    }
}
