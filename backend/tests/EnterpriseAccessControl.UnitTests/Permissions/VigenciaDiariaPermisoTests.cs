using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Permissions;
using FluentAssertions;
using NodaTime;

namespace EnterpriseAccessControl.UnitTests.Permissions;

/// <summary>
/// Vigencia diaria del permiso de acceso (RF-083 (a), (d), (f); CS-044, CS-045; F-1, F-6).
/// </summary>
/// <remarks>
/// Se usan zonas tzdb reales: el objetivo es precisamente que los límites de día salgan de tzdb y no de
/// aritmética propia. Lima no tiene cambio de horario; Santiago sí, y sus días de transición se localizan
/// con <see cref="DateTimeZone.GetZoneIntervals(Instant, Instant)"/> en lugar de codificar fechas u offsets,
/// con una aserción previa que garantiza la premisa de cada caso.
/// </remarks>
public sealed class VigenciaDiariaPermisoTests
{
    private static readonly DateTimeZone Lima = VigenciaDiariaPermiso.Zona("America/Lima");
    private static readonly DateTimeZone Santiago = VigenciaDiariaPermiso.Zona("America/Santiago");

    private static readonly DateOnly Dia25 = new(2026, 9, 25);
    private static readonly DateOnly Dia30 = new(2026, 9, 30);

    // --- Conversión (F-1) -----------------------------------------------------------------------

    [Fact]
    public void En_Lima_el_inicio_es_las_00_00_locales_en_UTC()
    {
        var inicio = VigenciaDiariaPermiso.InicioUtc(Dia25, Lima);

        inicio.Should().Be(new DateTime(2026, 9, 25, 5, 0, 0, DateTimeKind.Utc));
        inicio.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void En_Lima_el_fin_es_el_inicio_del_dia_siguiente_menos_un_milisegundo()
    {
        var fin = VigenciaDiariaPermiso.FinUtc(Dia30, Lima);

        fin.Should().Be(new DateTime(2026, 10, 1, 4, 59, 59, 999, DateTimeKind.Utc));
        fin.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Un_permiso_de_un_solo_dia_tiene_fin_posterior_al_inicio()
    {
        // CS-045: inicio = fin es una vigencia válida de un día completo.
        var inicio = VigenciaDiariaPermiso.InicioUtc(Dia25, Lima);
        var fin = VigenciaDiariaPermiso.FinUtc(Dia25, Lima);

        (fin - inicio + TimeSpan.FromMilliseconds(1)).Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void En_Santiago_el_dia_sin_00_00_empieza_en_su_primer_instante_valido_y_dura_23_horas()
    {
        var dia = PrimerDiaConTransicion(Santiago, adelanta: true);

        // Premisa: ese día las 00:00 no existen (el reloj salta de 00:00 a 01:00).
        Santiago.AtStartOfDay(dia).TimeOfDay.Should().NotBe(LocalTime.Midnight);

        var fecha = dia.ToDateOnly();
        var inicio = VigenciaDiariaPermiso.InicioUtc(fecha, Santiago);
        var fin = VigenciaDiariaPermiso.FinUtc(fecha, Santiago);

        Instant.FromDateTimeUtc(inicio).InZone(Santiago).TimeOfDay.Should().Be(new LocalTime(1, 0));
        (fin - inicio + TimeSpan.FromMilliseconds(1)).Should().Be(TimeSpan.FromHours(23));

        // El día anterior termina 1 ms antes del primer instante válido.
        VigenciaDiariaPermiso.FinUtc(fecha.AddDays(-1), Santiago)
            .Should().Be(inicio.AddMilliseconds(-1));
    }

    [Fact]
    public void En_Santiago_el_dia_con_una_hora_repetida_dura_25_horas()
    {
        var dia = PrimerDiaConTransicion(Santiago, adelanta: false);
        var fecha = dia.ToDateOnly();

        var inicio = VigenciaDiariaPermiso.InicioUtc(fecha, Santiago);
        var fin = VigenciaDiariaPermiso.FinUtc(fecha, Santiago);

        (fin - inicio + TimeSpan.FromMilliseconds(1)).Should().Be(TimeSpan.FromHours(25));
        VigenciaDiariaPermiso.FinUtc(fecha.AddDays(-1), Santiago)
            .Should().Be(inicio.AddMilliseconds(-1));
    }

    [Fact]
    public void Un_dia_omitido_por_la_zona_se_rechaza_como_entrada_invalida()
    {
        // Samoa omitió el 30/12/2011 al cruzar la línea de cambio de fecha.
        var apia = VigenciaDiariaPermiso.Zona("Pacific/Apia");

        var convertir = () => VigenciaDiariaPermiso.InicioUtc(new DateOnly(2011, 12, 30), apia);

        convertir.Should().Throw<ReglaNegocioInvalidaException>()
            .Which.Codigo.Should().Be(CodigosError.ValidacionEntrada);
    }

    // --- Fecha civil y días completos (F-5, F-7) ------------------------------------------------

    [Fact]
    public void La_fecha_civil_de_instantes_antiguos_con_hora_es_su_dia_local()
    {
        // CS-046: 25/09 03:00 y 30/09 12:00 en Lima.
        VigenciaDiariaPermiso.FechaCivil(new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc), Lima)
            .Should().Be(Dia25);
        VigenciaDiariaPermiso.FechaCivil(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), Lima)
            .Should().Be(Dia30);
    }

    [Fact]
    public void La_fecha_civil_del_fin_normalizado_es_el_propio_dia_de_fin()
    {
        VigenciaDiariaPermiso.FechaCivil(VigenciaDiariaPermiso.FinUtc(Dia30, Lima), Lima).Should().Be(Dia30);
    }

    [Fact]
    public void Una_vigencia_normalizada_es_de_dias_completos()
    {
        VigenciaDiariaPermiso.EsDiaCompleto(
                VigenciaDiariaPermiso.InicioUtc(Dia25, Lima),
                VigenciaDiariaPermiso.FinUtc(Dia30, Lima),
                Lima)
            .Should().BeTrue();
    }

    [Fact]
    public void Una_vigencia_antigua_con_hora_no_es_de_dias_completos()
    {
        VigenciaDiariaPermiso.EsDiaCompleto(
                new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                Lima)
            .Should().BeFalse();
    }

    [Fact]
    public void Una_vigencia_normalizada_en_Lima_deja_de_ser_de_dias_completos_en_Santiago()
    {
        // F-7: cambiar la zona no reescribe instantes; solo cambia su lectura.
        VigenciaDiariaPermiso.EsDiaCompleto(
                VigenciaDiariaPermiso.InicioUtc(Dia25, Lima),
                VigenciaDiariaPermiso.FinUtc(Dia30, Lima),
                Santiago)
            .Should().BeFalse();
    }

    // --- Edición por extremo (F-6) --------------------------------------------------------------

    [Fact]
    public void Un_inicio_cuya_fecha_no_cambia_conserva_su_instante_exacto()
    {
        var almacenado = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);

        var (instante, cambia) = VigenciaDiariaPermiso.ResolverExtremo(almacenado, Dia25, Lima, esFin: false);

        instante.Should().Be(almacenado);
        cambia.Should().BeFalse();
    }

    [Fact]
    public void Un_fin_cuya_fecha_no_cambia_conserva_su_instante_exacto()
    {
        var almacenado = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);

        var (instante, cambia) = VigenciaDiariaPermiso.ResolverExtremo(almacenado, Dia30, Lima, esFin: true);

        instante.Should().Be(almacenado);
        cambia.Should().BeFalse();
    }

    [Fact]
    public void Un_inicio_con_otra_fecha_se_normaliza_al_inicio_de_ese_dia()
    {
        var almacenado = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);
        var nueva = new DateOnly(2026, 9, 24);

        var (instante, cambia) = VigenciaDiariaPermiso.ResolverExtremo(almacenado, nueva, Lima, esFin: false);

        instante.Should().Be(new DateTime(2026, 9, 24, 5, 0, 0, DateTimeKind.Utc));
        cambia.Should().BeTrue();
    }

    [Fact]
    public void Un_fin_con_otra_fecha_se_normaliza_al_final_de_ese_dia()
    {
        var almacenado = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);
        var nueva = new DateOnly(2026, 10, 5);

        var (instante, cambia) = VigenciaDiariaPermiso.ResolverExtremo(almacenado, nueva, Lima, esFin: true);

        instante.Should().Be(new DateTime(2026, 10, 6, 4, 59, 59, 999, DateTimeKind.Utc));
        cambia.Should().BeTrue();
    }

    /// <summary>
    /// Primer día local, a partir de 2026, en que la zona adelanta (hueco) o atrasa (repetición) el reloj.
    /// </summary>
    private static LocalDate PrimerDiaConTransicion(DateTimeZone zona, bool adelanta)
    {
        var desde = Instant.FromUtc(2026, 1, 1, 0, 0);
        var hasta = Instant.FromUtc(2028, 1, 1, 0, 0);

        var intervalos = zona.GetZoneIntervals(desde, hasta).ToList();

        for (var i = 1; i < intervalos.Count; i++)
        {
            var anterior = intervalos[i - 1].WallOffset;
            var siguiente = intervalos[i].WallOffset;

            if (adelanta ? siguiente > anterior : siguiente < anterior)
            {
                return intervalos[i].Start.InZone(zona).Date;
            }
        }

        throw new InvalidOperationException($"La zona {zona.Id} no tiene la transición buscada entre 2026 y 2027.");
    }
}
