using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Gate de credencial vigente, paso 6 del algoritmo (RF-066, RF-070, RF-071; research.md §7, §24).
/// </summary>
/// <remarks>
/// Todas parten de una persona con contexto operativo, perfil, unidad y permiso aplicable vigentes:
/// lo único que cambia entre casos es la credencial, de modo que el motivo de denegación solo puede
/// venir de ella.
///
/// Los estados de cierre se escriben directamente en la base de datos: devolver y dar de baja una
/// credencial por la API es trabajo de la Historia 9, y aquí solo interesa cómo los lee la evaluación.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class GateCredencialTests(SqlServerFixture fixture)
{
    /// <summary>Escenario completo sin credencial, con un permiso personal que cubre toda la semana.</summary>
    private async Task<EscenarioPermisos> MontarSinCredencialAsync(bool comoContratista = false)
    {
        var escenario = await new EscenarioPermisos(fixture)
            .MontarAsync(comoContratista, conCredencial: false);

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: escenario.FechaCivil(EscenarioPermisos.Instante.AddMonths(-1)),
            fin: escenario.FechaCivil(EscenarioPermisos.Instante.AddMonths(6)),
            bloques: EscenarioPermisos.BloquesTodaLaSemana()));

        return escenario;
    }

    [Fact]
    public async Task Con_credencial_vigente_concede()
    {
        // Contrapunto de todos los demás casos: sin él, una denegación podría venir de otro paso.
        var escenario = await MontarSinCredencialAsync();
        await escenario.Us5.AsignarCredencialAsync(escenario.PrincipalA.Id);

        (await escenario.EvaluarAsync()).Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
    }

    [Fact]
    public async Task Caso_1_una_credencial_nunca_asignada_deniega()
    {
        var escenario = await MontarSinCredencialAsync();

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.DENEGADO);
        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Fact]
    public async Task Una_credencial_de_otra_principal_no_sirve_para_esta()
    {
        // RF-056: la credencial es por Principal. Tener la de B no habilita un área de A.
        // Persona de contratista, que es quien puede tener contexto con ambas (RF-052, RF-054).
        var escenario = await MontarSinCredencialAsync(comoContratista: true);

        await escenario.Us5.ContextoAsync(escenario.PrincipalB.Id);
        await escenario.Us5.AsignarCredencialAsync(escenario.PrincipalB.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Theory]
    [InlineData(EstadoCredencial.DEVUELTO)]
    [InlineData(EstadoCredencial.ELIMINADO)]
    [InlineData(EstadoCredencial.REVOCADA)]
    public async Task Caso_2_una_credencial_en_estado_de_cierre_deniega_aunque_su_ventana_siga_abierta(
        EstadoCredencial estado)
    {
        var escenario = await MontarSinCredencialAsync();
        var credencial = await escenario.Us5.AsignarCredencialAsync(escenario.PrincipalA.Id);

        await CambiarEstadoAsync(credencial.Id, estado);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Fact]
    public async Task Caso_3_una_credencial_asignada_pero_expirada_deniega_y_no_cambia_de_estado()
    {
        var escenario = await MontarSinCredencialAsync();

        // Vigente hoy, expirada en el instante evaluado (mañana): el fin se normaliza al final del día.
        var credencial = await escenario.Us5.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            fin: EscenarioPermisos.Instante.AddDays(-1));

        var antes = await LeerAsync(credencial.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);

        // research.md §24: la evaluación lee, no escribe. El mero vencimiento no transiciona el estado.
        var despues = await LeerAsync(credencial.Id);

        despues.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        despues.FechaHoraFin.Should().Be(antes.FechaHoraFin);
        despues.UpdatedAt.Should().Be(antes.UpdatedAt);
    }

    [Fact]
    public async Task La_credencial_es_vigente_hasta_el_ultimo_milisegundo_de_su_fin_inclusive()
    {
        // RF-070: FechaHoraInicio <= fecha evaluada <= FechaHoraFin, ambos extremos inclusivos.
        var escenario = await MontarSinCredencialAsync();

        var credencial = await escenario.Us5.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            fin: EscenarioPermisos.Instante);

        // Fin normalizado a las 23:59:59.999 UTC del día evaluado, que en Lima son las 18:59:59.999:
        // se evalúa con un permiso que cubra esa hora.
        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques: [.. Enum.GetValues<DiaSemana>()
                .Select(d => new Application.Permissions.BloqueHorarioRequest(d, "18:00", "19:00"))]));

        (await escenario.EvaluarAsync(fechaHora: credencial.FechaHoraFin))
            .Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        (await escenario.EvaluarAsync(fechaHora: credencial.FechaHoraFin.AddMilliseconds(1)))
            .MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Fact]
    public async Task Con_una_credencial_vigente_y_otra_futura_se_usa_la_vigente_en_el_instante_evaluado()
    {
        // Defecto corregido: el orquestador elegía la credencial de inicio más reciente sin mirar el
        // instante evaluado. Con la del próximo periodo ya registrada, denegaba a quien hoy sí tiene
        // una credencial vigente.
        var escenario = await MontarSinCredencialAsync();

        var vigente = await escenario.Us5.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            fin: EscenarioPermisos.Instante.AddDays(1));

        var futura = await escenario.Us5.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            inicio: EscenarioPermisos.Instante.AddDays(3),
            fin: EscenarioPermisos.Instante.AddMonths(2));

        // Premisas: misma Principal, ambas ASIGNADO, sin solaparse, y la futura empieza después.
        futura.CompaniaPrincipalId.Should().Be(vigente.CompaniaPrincipalId);
        vigente.FechaHoraFin.Should().BeBefore(futura.FechaHoraInicio);
        futura.FechaHoraInicio.Should().BeAfter(EscenarioPermisos.Instante);

        // En el instante evaluado solo la primera está vigente, y basta para conceder.
        (await escenario.EvaluarAsync()).Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        // En el hueco entre ambas no hay credencial vigente.
        (await escenario.EvaluarAsync(fechaHora: EscenarioPermisos.Instante.AddDays(2)))
            .MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);

        // Y cuando empieza la futura, es ella la que concede.
        (await escenario.EvaluarAsync(fechaHora: EscenarioPermisos.Instante.AddDays(3)))
            .Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
    }

    [Fact]
    public async Task Una_credencial_cerrada_no_oculta_otra_vigente_de_la_misma_principal()
    {
        // La devuelta es la más reciente por inicio, pero no es la vigente: no debe ser la elegida.
        var escenario = await MontarSinCredencialAsync();

        var anterior = await escenario.Us5.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            fin: EscenarioPermisos.Instante.AddMonths(1));

        var devuelta = await escenario.Us5.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            inicio: EscenarioPermisos.Instante.AddMonths(2),
            fin: EscenarioPermisos.Instante.AddMonths(3));

        await CambiarEstadoAsync(devuelta.Id, EstadoCredencial.DEVUELTO);

        anterior.FechaHoraInicio.Should().BeBefore(devuelta.FechaHoraInicio);

        (await escenario.EvaluarAsync()).Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
    }

    private Task CambiarEstadoAsync(Guid credencialId, EstadoCredencial estado) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            var credencial = await db.Set<AsignacionCredencial>().SingleAsync(c => c.Id == credencialId);
            credencial.Estado = estado;
            await db.SaveChangesAsync();
        });

    private async Task<AsignacionCredencial> LeerAsync(Guid credencialId)
    {
        AsignacionCredencial? credencial = null;

        await fixture.Api.ConDbContextAsync(async db =>
            credencial = await db.Set<AsignacionCredencial>().AsNoTracking()
                .SingleAsync(c => c.Id == credencialId));

        return credencial!;
    }
}
