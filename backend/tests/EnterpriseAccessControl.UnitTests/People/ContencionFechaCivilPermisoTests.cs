using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.People;

/// <summary>
/// Contención de RF-082 por fecha civil para <c>PermisoAcceso</c> (RF-083 (c); F-2).
/// </summary>
/// <remarks>
/// La pertenencia se construye con <see cref="Vigencia.NormalizarRango"/>, como en producción: guarda el
/// día declarado en sus límites UTC, y su fecha declarada son esos componentes. La garantía de que el mismo
/// día civil no se rechaza por la diferencia de instantes UTC entre la zona del permiso y la pertenencia se
/// prueba en el punto de llamada del servicio (<c>ContencionPermisoFechaCivilTests</c>, T300): esta
/// función solo recibe fechas.
/// </remarks>
public sealed class ContencionFechaCivilPermisoTests
{
    private static readonly DateOnly Desde = new(2026, 8, 1);
    private static readonly DateOnly Hasta = new(2027, 7, 31);

    private static AsignacionPersonaCompania Pertenencia()
    {
        var (inicio, fin) = Vigencia.NormalizarRango(
            Desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Hasta.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        return new AsignacionPersonaCompania
        {
            PersonaId = Guid.CreateVersion7(),
            CompaniaId = Guid.CreateVersion7(),
            FechaHoraInicio = inicio,
            FechaHoraFin = fin,
        };
    }

    [Fact]
    public void Las_mismas_fechas_que_la_pertenencia_son_validas()
    {
        var validar = () => ContencionTemporalValidator.ValidarFechasCiviles(Pertenencia(), Desde, Hasta);

        validar.Should().NotThrow();
    }

    [Fact]
    public void Un_fin_posterior_al_ultimo_dia_de_la_pertenencia_se_rechaza()
    {
        var validar = () => ContencionTemporalValidator.ValidarFechasCiviles(
            Pertenencia(), Desde, Hasta.AddDays(1));

        validar.Should().Throw<ConflictoEstadoException>()
            .Which.Codigo.Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public void Un_inicio_anterior_al_primer_dia_de_la_pertenencia_se_rechaza()
    {
        var validar = () => ContencionTemporalValidator.ValidarFechasCiviles(
            Pertenencia(), Desde.AddDays(-1), Hasta);

        validar.Should().Throw<ConflictoEstadoException>()
            .Which.Codigo.Should().Be(CodigosError.FueraDeContencionTemporal);
    }
}
