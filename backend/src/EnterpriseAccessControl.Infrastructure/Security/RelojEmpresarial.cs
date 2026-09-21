using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Common;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Extensions;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Implementación con NodaTime de la conversión UTC ↔ zona de una Compañía Principal
/// (research.md §5, §31; RF-080).
/// </summary>
/// <remarks>
/// Se usa NodaTime en lugar de <c>TimeZoneInfo</c> porque expone explícitamente los casos
/// ambiguos/inexistentes de los cambios de horario y usa identificadores IANA
/// (<c>America/Lima</c>), estables entre Windows y Linux — el despliegue objetivo es Linux en
/// contenedor (plan.md, Target Platform).
///
/// La zona global configurada dejó de ser "la zona del sistema" y pasó a ser **el respaldo**: rige
/// para las entidades cuya vigencia no es resoluble a una única Compañía Principal y para cualquier
/// Principal que todavía no tenga zona propia. El servicio sigue siendo singleton porque ya no
/// guarda una zona de negocio, solo resuelve identificadores contra la base de datos tzdb.
/// </remarks>
public sealed class RelojEmpresarial : IRelojEmpresarial, IRelojSistema
{
    private readonly DateTimeZone _zonaRespaldo;

    public RelojEmpresarial(IOptions<ZonaHorariaOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _zonaRespaldo =
            DateTimeZoneProviders.Tzdb.GetZoneOrNull(opciones.Value.TimeZoneId)
            ?? throw new InvalidOperationException(
                $"Zona horaria global de respaldo no reconocida: '{opciones.Value.TimeZoneId}'.");
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DayOfWeek DiaSemanaLocal(DateTime instanteUtc, string? zonaIana) =>
        EnZona(instanteUtc, zonaIana).DayOfWeek.ToDayOfWeek();

    public TimeOnly HoraLocal(DateTime instanteUtc, string? zonaIana)
    {
        var local = EnZona(instanteUtc, zonaIana);
        return new TimeOnly(local.Hour, local.Minute, local.Second);
    }

    public string ZonaEfectiva(string? zonaIana) => Resolver(zonaIana).Id;

    public bool EsZonaValida(string? zonaIana) =>
        !string.IsNullOrWhiteSpace(zonaIana)
        && DateTimeZoneProviders.Tzdb.GetZoneOrNull(zonaIana) is not null;

    /// <summary>
    /// Resuelve la zona a aplicar, cayendo en el respaldo global cuando no hay una utilizable.
    /// </summary>
    /// <remarks>
    /// Una zona desconocida cae en el respaldo en lugar de lanzar: la validación del identificador
    /// ocurre al guardar la compañía (RF-080), y hacer fallar una evaluación de acceso por un dato
    /// de configuración inválido convertiría un error administrativo en una denegación con un
    /// motivo engañoso.
    /// </remarks>
    private DateTimeZone Resolver(string? zonaIana)
    {
        if (string.IsNullOrWhiteSpace(zonaIana))
        {
            return _zonaRespaldo;
        }

        return DateTimeZoneProviders.Tzdb.GetZoneOrNull(zonaIana) ?? _zonaRespaldo;
    }

    private LocalDateTime EnZona(DateTime instanteUtc, string? zonaIana)
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
        return instant.InZone(Resolver(zonaIana)).LocalDateTime;
    }
}
