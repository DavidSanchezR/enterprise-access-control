using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Common;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Extensions;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Implementación con NodaTime de la conversión UTC ↔ zona horaria empresarial (research.md §5).
/// </summary>
/// <remarks>
/// Se usa NodaTime en lugar de <c>TimeZoneInfo</c> porque expone explícitamente los casos
/// ambiguos/inexistentes de los cambios de horario y usa identificadores IANA
/// (<c>America/Lima</c>), estables entre Windows y Linux — el despliegue objetivo es Linux en
/// contenedor (plan.md, Target Platform).
/// </remarks>
public sealed class RelojEmpresarial : IRelojEmpresarial, IRelojSistema
{
    private readonly DateTimeZone _zona;

    public RelojEmpresarial(IOptions<ZonaHorariaOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _zona =
            DateTimeZoneProviders.Tzdb.GetZoneOrNull(opciones.Value.TimeZoneId)
            ?? throw new InvalidOperationException(
                $"Zona horaria empresarial no reconocida: '{opciones.Value.TimeZoneId}'.");
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DayOfWeek DiaSemanaLocal(DateTime instanteUtc) =>
        EnZonaEmpresarial(instanteUtc).DayOfWeek.ToDayOfWeek();

    public TimeOnly HoraLocal(DateTime instanteUtc)
    {
        var local = EnZonaEmpresarial(instanteUtc);
        return new TimeOnly(local.Hour, local.Minute, local.Second);
    }

    private LocalDateTime EnZonaEmpresarial(DateTime instanteUtc)
    {
        // Se exige UTC explícito: aceptar un DateTime de Kind desconocido permitiría que una fecha
        // local se interpretara silenciosamente como UTC y desplazara la evaluación horaria.
        if (instanteUtc.Kind == DateTimeKind.Local)
        {
            throw new ArgumentException(
                "El instante evaluado debe expresarse en UTC (Constitución, Reglas de Arquitectura e Ingeniería).",
                nameof(instanteUtc));
        }

        var instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc));
        return instant.InZone(_zona).LocalDateTime;
    }
}
