using FluentValidation;

namespace EnterpriseAccessControl.Application.People.Validators;

/// <summary>
/// Validación de borde de las asociaciones temporales de persona (RF-071, contracts/people.yaml).
/// </summary>
/// <remarks>
/// **Por qué hacen falta, además de la normalización del servicio.** Sin un validador, omitir
/// <c>fechaHoraFin</c> en el cuerpo JSON la enlaza al valor por defecto de <c>DateTime</c>
/// (0001-01-01) en lugar de rechazarse: el error aflora más tarde, al normalizar el rango, y sale
/// como <c>409 PERIODO_INVALIDO</c>. Pero el problema no es un período inválido sino un campo
/// ausente, y el contrato lo declara obligatorio — corresponde un <c>400</c>.
///
/// La comprobación es de <em>presencia</em>, no de contenido: las reglas de negocio sobre esas
/// fechas —contención temporal, no-solapamiento, orden— siguen viviendo en los servicios, que son
/// quienes conocen la pertenencia que las enmarca.
/// </remarks>
internal static class ReglasFechas
{
    private const string FechaFinAusente =
        "La fecha de fin de vigencia es obligatoria: no existe vigencia indefinida (RF-071).";

    private const string FechaInicioAusente = "La fecha de inicio de vigencia es obligatoria.";

    /// <summary>Exige que ambas fechas vengan informadas con un valor real.</summary>
    /// <remarks>
    /// Se compara contra <c>default</c> porque es exactamente el valor al que el deserializador
    /// enlaza un campo ausente en un parámetro posicional de <c>record</c>.
    /// </remarks>
    public static void ExigirVigencia<T>(
        this AbstractValidator<T> validador,
        Func<T, DateTime> inicio,
        Func<T, DateTime> fin)
    {
        validador.RuleFor(r => inicio(r))
            .NotEqual(default(DateTime)).WithName("fechaHoraInicio").WithMessage(FechaInicioAusente);

        validador.RuleFor(r => fin(r))
            .NotEqual(default(DateTime)).WithName("fechaHoraFin").WithMessage(FechaFinAusente);
    }
}

/// <summary>Alta de pertenencia Persona–Compañía (RF-014, RF-071).</summary>
public sealed class AsignacionCompaniaRequestValidator : AbstractValidator<AsignacionCompaniaRequest>
{
    public AsignacionCompaniaRequestValidator()
    {
        RuleFor(r => r.CompaniaId)
            .NotEqual(Guid.Empty).WithMessage("La compañía es obligatoria.");

        this.ExigirVigencia(r => r.FechaHoraInicio, r => r.FechaHoraFin);
    }
}

/// <summary>Apertura de contexto operativo (RF-052 a RF-054, RF-071).</summary>
public sealed class ContextoOperativoRequestValidator : AbstractValidator<ContextoOperativoRequest>
{
    public ContextoOperativoRequestValidator()
    {
        RuleFor(r => r.CompaniaPrincipalId)
            .NotEqual(Guid.Empty).WithMessage("La compañía principal es obligatoria.");

        this.ExigirVigencia(r => r.FechaHoraInicio, r => r.FechaHoraFin);
    }
}

/// <summary>Asignación de unidad organizativa dentro de un contexto (RF-015, RF-055, RF-071).</summary>
public sealed class AsignacionUnidadOrganizativaRequestValidator
    : AbstractValidator<AsignacionUnidadOrganizativaRequest>
{
    public AsignacionUnidadOrganizativaRequestValidator()
    {
        RuleFor(r => r.UnidadOrganizativaId)
            .NotEqual(Guid.Empty).WithMessage("La unidad organizativa es obligatoria.");

        this.ExigirVigencia(r => r.FechaHoraInicio, r => r.FechaHoraFin);
    }
}

/// <summary>Asignación de perfil (RF-011, RF-071).</summary>
/// <remarks>
/// RF-071 le aplica —la fecha de fin es obligatoria—, pero RF-072 no: el perfil no depende de la
/// pertenencia, así que aquí no hay nada que contener.
/// </remarks>
public sealed class AsignacionTipoPersonaRequestValidator
    : AbstractValidator<AsignacionTipoPersonaRequest>
{
    public AsignacionTipoPersonaRequestValidator()
    {
        RuleFor(r => r.TipoPersonaId)
            .NotEqual(Guid.Empty).WithMessage("El tipo de persona es obligatorio.");

        this.ExigirVigencia(r => r.FechaHoraInicio, r => r.FechaHoraFin);
    }
}

/// <summary>Cese explícito de pertenencia (RF-061).</summary>
public sealed class FinalizarPertenenciaRequestValidator
    : AbstractValidator<FinalizarPertenenciaRequest>
{
    public FinalizarPertenenciaRequestValidator() =>
        RuleFor(r => r.FechaHoraFin)
            .NotEqual(default(DateTime))
            .WithMessage("La fecha efectiva de cese es obligatoria.");
}

/// <summary>Renovación de pertenencia (RF-073).</summary>
public sealed class RenovarPertenenciaRequestValidator : AbstractValidator<RenovarPertenenciaRequest>
{
    public RenovarPertenenciaRequestValidator() =>
        RuleFor(r => r.FechaHoraFin)
            .NotEqual(default(DateTime))
            .WithMessage("La nueva fecha de fin de vigencia es obligatoria.");
}
