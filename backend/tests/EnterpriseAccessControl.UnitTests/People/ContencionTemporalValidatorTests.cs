using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.People;

/// <summary>
/// Contención temporal jerárquica (RF-072), aislada de la base de datos.
/// </summary>
/// <remarks>
/// La regla es que una asociación dependiente quepa íntegra dentro de la vigencia de la pertenencia.
/// Los casos límite —igualdad exacta en cada extremo— son los que deciden si la regla es utilizable:
/// si la igualdad se rechazara, el caso más común (una asociación que dura exactamente lo mismo que
/// la pertenencia) sería imposible de registrar.
/// </remarks>
public sealed class ContencionTemporalValidatorTests
{
    private static readonly DateTime Inicio = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Fin = new(2027, 7, 31, 23, 59, 59, 999, DateTimeKind.Utc);

    private static AsignacionPersonaCompania Pertenencia() => new()
    {
        PersonaId = Guid.CreateVersion7(),
        CompaniaId = Guid.CreateVersion7(),
        FechaHoraInicio = Inicio,
        FechaHoraFin = Fin,
    };

    [Fact]
    public void Un_rango_estrictamente_interior_es_valido()
    {
        var validar = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio.AddMonths(1), Fin.AddMonths(-1));

        validar.Should().NotThrow();
    }

    [Fact]
    public void Un_rango_identico_a_la_pertenencia_es_valido()
    {
        // CS-034: el caso más común. Rechazarlo haría la regla inutilizable en la práctica.
        var validar = () => ContencionTemporalValidator.Validar(Pertenencia(), Inicio, Fin);

        validar.Should().NotThrow();
    }

    [Fact]
    public void La_igualdad_exacta_en_el_inicio_es_valida()
    {
        var validar = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio, Fin.AddMonths(-3));

        validar.Should().NotThrow();
    }

    [Fact]
    public void La_igualdad_exacta_en_el_fin_es_valida()
    {
        var validar = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio.AddMonths(3), Fin);

        validar.Should().NotThrow();
    }

    [Fact]
    public void Un_inicio_anterior_al_de_la_pertenencia_se_rechaza()
    {
        var validar = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio.AddMilliseconds(-1), Fin);

        validar.Should().Throw<ConflictoEstadoException>()
            .Which.Codigo.Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public void Un_fin_posterior_al_de_la_pertenencia_se_rechaza()
    {
        // Es el caso que la regla existe para impedir: una asociación que sobreviviría a la
        // pertenencia que la justifica.
        var validar = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio, Fin.AddMilliseconds(1));

        validar.Should().Throw<ConflictoEstadoException>()
            .Which.Codigo.Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public void Un_rango_completamente_fuera_se_rechaza()
    {
        var validar = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Fin.AddDays(1), Fin.AddDays(30));

        validar.Should().Throw<ConflictoEstadoException>();
    }

    [Fact]
    public void El_mensaje_distingue_si_el_exceso_es_por_el_inicio_o_por_el_fin()
    {
        // El usuario debe poder corregir el campo correcto sin adivinar.
        var porInicio = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio.AddDays(-1), Fin);

        porInicio.Should().Throw<ConflictoEstadoException>()
            .WithMessage("*comenzar antes*");

        var porFin = () => ContencionTemporalValidator.Validar(
            Pertenencia(), Inicio, Fin.AddDays(1));

        porFin.Should().Throw<ConflictoEstadoException>()
            .WithMessage("*más allá*");
    }
}
