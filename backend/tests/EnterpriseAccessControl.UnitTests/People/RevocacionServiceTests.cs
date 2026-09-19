using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.People;

/// <summary>
/// Reglas de cierre que aplica la revocación en cascada a cada fila (RF-061 a RF-065,
/// research.md §14.1), aisladas de la base de datos.
/// </summary>
/// <remarks>
/// Lo que se fija aquí es el <em>cómo</em> se cierra cada fila afectada. El <em>qué</em> filas
/// alcanza la cascada —la consulta y su atomicidad— se verifica en las pruebas de integración contra
/// SQL Server real, que es donde esa parte puede fallar de verdad.
/// </remarks>
public sealed class RevocacionServiceTests
{
    private static readonly Guid PertenenciaId = Guid.CreateVersion7();
    private static readonly DateTime Cese = new(2026, 9, 30, 23, 59, 59, 999, DateTimeKind.Utc);

    private static ContextoOperativoPersonaPrincipal Contexto(DateTime fin) => new()
    {
        PersonaId = Guid.CreateVersion7(),
        CompaniaPrincipalId = Guid.CreateVersion7(),
        FechaHoraInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaHoraFin = fin,
    };

    private static AsignacionCredencial Credencial(DateTime fin) => new()
    {
        PersonaId = Guid.CreateVersion7(),
        CompaniaPrincipalId = Guid.CreateVersion7(),
        TipoCredencialId = Guid.CreateVersion7(),
        FechaHoraInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaHoraFin = fin,
    };

    [Fact]
    public void La_fecha_de_fin_se_acorta_hasta_la_del_cese()
    {
        var contexto = Contexto(new DateTime(2027, 6, 30, 23, 59, 59, 999, DateTimeKind.Utc));

        ReglasRevocacion.Cerrar(
            contexto, Cese, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, PertenenciaId);

        contexto.FechaHoraFin.Should().Be(Cese);
    }

    [Fact]
    public void Una_fecha_de_fin_anterior_al_cese_no_se_extiende()
    {
        // La regla es acortar, nunca alargar: extenderla concedería una vigencia que nadie otorgó.
        var yaTerminaba = new DateTime(2026, 5, 31, 23, 59, 59, 999, DateTimeKind.Utc);
        var contexto = Contexto(yaTerminaba);

        ReglasRevocacion.Cerrar(
            contexto, Cese, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, PertenenciaId);

        contexto.FechaHoraFin.Should().Be(yaTerminaba);
    }

    [Fact]
    public void La_fecha_de_inicio_nunca_se_modifica()
    {
        var contexto = Contexto(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        var inicioOriginal = contexto.FechaHoraInicio;

        ReglasRevocacion.Cerrar(
            contexto, Cese, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, PertenenciaId);

        // El histórico debe seguir diciendo cuándo empezó (RF-063, CS-026).
        contexto.FechaHoraInicio.Should().Be(inicioOriginal);
    }

    [Fact]
    public void Una_revocacion_en_cascada_registra_estado_motivo_y_origen()
    {
        var contexto = Contexto(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        ReglasRevocacion.Cerrar(
            contexto, Cese, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, PertenenciaId);

        contexto.Estado.Should().Be(Estado.INACTIVO);
        contexto.MotivoFin.Should().Be(MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA);
        contexto.RevocadoPorPertenenciaId.Should().Be(PertenenciaId);
    }

    [Theory]
    [InlineData(MotivoFinRevocacion.REEMPLAZO_ASIGNACION)]
    [InlineData(MotivoFinRevocacion.CIERRE_MANUAL)]
    public void Un_cierre_que_no_es_cascada_no_registra_pertenencia_de_origen(
        MotivoFinRevocacion motivo)
    {
        var contexto = Contexto(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        ReglasRevocacion.Cerrar(contexto, Cese, motivo, PertenenciaId);

        // Poblar el origen aquí apuntaría a una pertenencia que no causó el cierre y falsearía la
        // auditoría.
        contexto.RevocadoPorPertenenciaId.Should().BeNull();
        contexto.MotivoFin.Should().Be(motivo);
    }

    [Fact]
    public void Una_credencial_revocada_queda_en_estado_REVOCADA_con_su_origen()
    {
        var credencial = Credencial(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        ReglasRevocacion.Revocar(credencial, Cese, PertenenciaId);

        // No lleva MotivoFin: el propio estado REVOCADA ya expresa la causa (research.md §14.2).
        credencial.Estado.Should().Be(EstadoCredencial.REVOCADA);
        credencial.RevocadoPorPertenenciaId.Should().Be(PertenenciaId);
        credencial.FechaHoraFin.Should().Be(Cese);
    }

    [Fact]
    public void Una_credencial_que_ya_terminaba_antes_conserva_su_fecha()
    {
        var yaTerminaba = new DateTime(2026, 6, 30, 23, 59, 59, 999, DateTimeKind.Utc);
        var credencial = Credencial(yaTerminaba);

        ReglasRevocacion.Revocar(credencial, Cese, PertenenciaId);

        credencial.FechaHoraFin.Should().Be(yaTerminaba);
        credencial.Estado.Should().Be(EstadoCredencial.REVOCADA);
    }

    [Fact]
    public void Un_cese_con_fecha_futura_propaga_esa_misma_fecha()
    {
        // RF-064: la cascada se ejecuta de inmediato pero con la fecha futura, de modo que el
        // dependiente sigue genuinamente vigente hasta que llegue. No se agenda ningún proceso.
        var futuro = DateTime.UtcNow.AddDays(15);
        var contexto = Contexto(DateTime.UtcNow.AddYears(1));

        ReglasRevocacion.Cerrar(
            contexto, futuro, MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA, PertenenciaId);

        contexto.FechaHoraFin.Should().Be(futuro);
        contexto.EstaVigenteEn(DateTime.UtcNow).Should().BeTrue(
            "el registro permanece utilizable hasta la fecha efectiva del cese");
    }

    [Theory]
    [InlineData("2027-06-30", "2026-09-30", "2026-09-30")]
    [InlineData("2026-05-31", "2026-09-30", "2026-05-31")]
    [InlineData("2026-09-30", "2026-09-30", "2026-09-30")]
    public void El_calculo_de_la_fecha_de_fin_toma_siempre_la_menor(
        string actual,
        string cese,
        string esperada)
    {
        var resultado = ReglasRevocacion.CalcularFechaFin(
            DateTime.Parse(actual, System.Globalization.CultureInfo.InvariantCulture),
            DateTime.Parse(cese, System.Globalization.CultureInfo.InvariantCulture));

        resultado.Should().Be(
            DateTime.Parse(esperada, System.Globalization.CultureInfo.InvariantCulture));
    }
}
