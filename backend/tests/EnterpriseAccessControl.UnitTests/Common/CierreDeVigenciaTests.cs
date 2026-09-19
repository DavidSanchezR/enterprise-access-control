using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.Common;

/// <summary>
/// Reglas comunes de cierre de vigencia, extraídas de los casos de uso que las repetían (T167).
/// </summary>
public sealed class CierreDeVigenciaTests
{
    private static readonly DateTime Inicio = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinDeclarado = new(2026, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc);

    [Fact]
    public void Un_cierre_anterior_al_fin_declarado_acorta_la_vigencia()
    {
        var cierre = new DateTime(2026, 6, 30, 23, 59, 59, 999, DateTimeKind.Utc);

        CierreDeVigencia.Acortar(FinDeclarado, cierre).Should().Be(cierre);
    }

    [Fact]
    public void Un_cierre_posterior_al_fin_declarado_nunca_extiende_la_vigencia()
    {
        CierreDeVigencia.Acortar(FinDeclarado, FinDeclarado.AddYears(1)).Should().Be(FinDeclarado);
    }

    [Theory]
    [InlineData(MotivoFinRevocacion.REEMPLAZO_ASIGNACION)]
    [InlineData(MotivoFinRevocacion.CIERRE_MANUAL)]
    [InlineData(MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA)]
    public void Cerrar_una_asociacion_fija_fin_estado_y_motivo_sin_tocar_el_inicio(MotivoFinRevocacion motivo)
    {
        var contexto = new ContextoOperativoPersonaPrincipal
        {
            PersonaId = Guid.CreateVersion7(),
            CompaniaPrincipalId = Guid.CreateVersion7(),
            FechaHoraInicio = Inicio,
            FechaHoraFin = FinDeclarado,
        };

        var origen = Guid.CreateVersion7();
        var cierre = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        CierreDeVigencia.Cerrar(contexto, cierre, motivo, origen);

        contexto.FechaHoraInicio.Should().Be(Inicio);
        contexto.FechaHoraFin.Should().Be(cierre);
        contexto.Estado.Should().Be(Estado.INACTIVO);
        contexto.MotivoFin.Should().Be(motivo);
        contexto.RevocadoPorPertenenciaId.Should().Be(origen);
    }

    [Theory]
    [InlineData(EstadoCredencial.DEVUELTO)]
    [InlineData(EstadoCredencial.ELIMINADO)]
    [InlineData(EstadoCredencial.REVOCADA)]
    public void Cerrar_una_credencial_la_lleva_al_estado_terminal_indicado(EstadoCredencial estado)
    {
        var credencial = Credencial();
        var cierre = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);

        CierreDeVigencia.Cerrar(credencial, cierre, estado, revocadoPorPertenenciaId: null);

        credencial.Estado.Should().Be(estado);
        credencial.FechaHoraFin.Should().Be(cierre);
        credencial.FechaHoraInicio.Should().Be(Inicio);
        credencial.RevocadoPorPertenenciaId.Should().BeNull();
    }

    [Fact]
    public void Cerrar_una_credencial_exige_un_estado_terminal()
    {
        var credencial = Credencial();

        var cerrar = () => CierreDeVigencia.Cerrar(credencial, DateTime.UtcNow, EstadoCredencial.ASIGNADO, null);

        cerrar.Should().Throw<ArgumentOutOfRangeException>();
        credencial.FechaHoraFin.Should().Be(FinDeclarado, "el rechazo no debe dejar la credencial a medias");
    }

    [Fact]
    public void Una_credencial_ya_expirada_conserva_su_fin_al_cerrarse_despues()
    {
        var credencial = Credencial();

        CierreDeVigencia.Cerrar(credencial, FinDeclarado.AddMonths(2), EstadoCredencial.DEVUELTO, null);

        credencial.FechaHoraFin.Should().Be(FinDeclarado);
    }

    [Fact]
    public void Una_fecha_sin_zona_se_interpreta_como_UTC_y_una_local_se_convierte()
    {
        var sinZona = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var local = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Local);

        InstanteUtc.Desde(sinZona).Should().Be(utc);
        InstanteUtc.Desde(sinZona).Kind.Should().Be(DateTimeKind.Utc);
        InstanteUtc.Desde(utc).Should().Be(utc);
        InstanteUtc.Desde(local).Should().Be(local.ToUniversalTime());
    }

    private static AsignacionCredencial Credencial() => new()
    {
        PersonaId = Guid.CreateVersion7(),
        CompaniaPrincipalId = Guid.CreateVersion7(),
        TipoCredencialId = Guid.CreateVersion7(),
        FechaHoraInicio = Inicio,
        FechaHoraFin = FinDeclarado,
    };
}
