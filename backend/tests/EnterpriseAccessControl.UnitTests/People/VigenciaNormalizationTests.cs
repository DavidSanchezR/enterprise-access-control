using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.People;

/// <summary>
/// Normalización de las ventanas de vigencia a días completos (RF-016, RF-039).
/// </summary>
public sealed class VigenciaNormalizationTests
{
    [Fact]
    public void El_inicio_se_lleva_al_comienzo_del_dia()
    {
        var normalizado = Vigencia.NormalizarInicio(
            new DateTime(2026, 8, 1, 14, 37, 22, DateTimeKind.Utc));

        // Sin esto, una vigencia registrada por la tarde dejaría sin cubrir la mañana de su
        // primer día.
        normalizado.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void El_fin_se_lleva_al_ultimo_instante_del_dia()
    {
        var normalizado = Vigencia.NormalizarFin(
            new DateTime(2027, 7, 31, 9, 5, 0, DateTimeKind.Utc));

        normalizado.Should().Be(
            new DateTime(2027, 7, 31, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Fact]
    public void El_fin_normalizado_queda_siempre_en_UTC()
    {
        // Persistir un Kind distinto produciría desplazamientos silenciosos al comparar vigencias.
        Vigencia.NormalizarFin(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Local))
            .Kind.Should().Be(DateTimeKind.Utc);

        Vigencia.NormalizarInicio(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Local))
            .Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Dos_vigencias_consecutivas_por_dias_no_se_solapan()
    {
        // Es la propiedad que hace que el trigger de no-solapamiento no rechace dos períodos
        // declarados en días contiguos.
        var finPrimera = Vigencia.NormalizarFin(new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc));
        var inicioSegunda = Vigencia.NormalizarInicio(new DateTime(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc));

        finPrimera.Should().BeBefore(inicioSegunda);
    }

    [Fact]
    public void Un_rango_de_un_solo_dia_es_valido()
    {
        var (inicio, fin) = Vigencia.NormalizarRango(
            new DateTime(2026, 8, 1, 23, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc));

        // Aunque la hora de fin recibida sea anterior a la de inicio, ambas son del mismo día: tras
        // normalizar, el rango cubre el día completo y es válido.
        inicio.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        fin.Should().Be(new DateTime(2026, 8, 1, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Fact]
    public void Un_rango_cuyo_fin_es_un_dia_anterior_al_inicio_se_rechaza()
    {
        var invertido = () => Vigencia.NormalizarRango(
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));

        invertido.Should().Throw<ConflictoEstadoException>()
            .Which.Codigo.Should().Be(CodigosError.PeriodoInvalido);
    }

    [Fact]
    public void El_ejemplo_de_negocio_se_normaliza_como_un_ano_completo()
    {
        // Ejemplo canónico de la especificación: 01/08/2026 a 31/07/2027.
        var (inicio, fin) = Vigencia.NormalizarRango(
            new DateTime(2026, 8, 1, 10, 30, 0, DateTimeKind.Utc),
            new DateTime(2027, 7, 31, 16, 45, 0, DateTimeKind.Utc));

        inicio.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        fin.Should().Be(new DateTime(2027, 7, 31, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Fact]
    public void La_normalizacion_es_idempotente()
    {
        var unaVez = Vigencia.NormalizarFin(new DateTime(2026, 8, 1, 3, 0, 0, DateTimeKind.Utc));
        var dosVeces = Vigencia.NormalizarFin(unaVez);

        // Renovar o cerrar repetidamente no debe ir desplazando la fecha.
        dosVeces.Should().Be(unaVez);
    }
}
