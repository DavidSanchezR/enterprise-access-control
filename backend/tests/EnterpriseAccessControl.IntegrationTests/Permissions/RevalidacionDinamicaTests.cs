using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Re-validación dinámica de la legitimidad del contexto operativo, paso 5 del algoritmo
/// (RF-065; research.md §7 paso 5, §14).
/// </summary>
/// <remarks>
/// En el sistema real, cambiar de compañía revoca en cascada los contextos dependientes (US5). Estas
/// pruebas simulan justo lo contrario —una inconsistencia histórica en la que el contexto quedó
/// abierto— escribiendo la pertenencia directamente en la base de datos, sin pasar por el servicio que
/// dispararía la cascada. La evaluación debe denegar igual: es una defensa adicional, no un sustituto
/// de la revocación.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RevalidacionDinamicaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Tras_pasar_a_otra_principal_sin_cascada_el_contexto_abierto_ya_no_legitima()
    {
        var escenario = await MontarConcedidoAsync(comoContratista: false);

        await CambiarPertenenciaSinCascadaAsync(escenario, escenario.PrincipalB.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.DENEGADO);
        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);

        await ContextoSigueIntactoAsync(escenario);
    }

    [Fact]
    public async Task Tras_pasar_a_una_contratista_sin_relacion_con_la_principal_deniega()
    {
        var escenario = await MontarConcedidoAsync(comoContratista: false);

        // Contratista nueva, sin RelaciónContratistaPrincipal con la Principal A.
        var ajena = await fixture.Api.SembrarCompaniaAsync(
            $"Contratista sin relación {Guid.CreateVersion7():N}"[..40], TipoCompania.CONTRATISTA);

        await CambiarPertenenciaSinCascadaAsync(escenario, ajena.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.RELACION_CONTRATISTA_PRINCIPAL_VENCIDA);

        await ContextoSigueIntactoAsync(escenario);
    }

    [Fact]
    public async Task Sin_pertenencia_vigente_en_la_fecha_evaluada_el_contexto_abierto_no_legitima()
    {
        var escenario = await MontarConcedidoAsync(comoContratista: true);

        // Se cierra la pertenencia antes del instante evaluado y no se abre otra.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var pertenencia = await db.Set<AsignacionPersonaCompania>()
                .SingleAsync(a => a.PersonaId == escenario.Persona.Id);

            pertenencia.FechaHoraFin = EscenarioPermisos.Instante.AddHours(-2);
            await db.SaveChangesAsync();
        });

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);

        await ContextoSigueIntactoAsync(escenario);
    }

    [Fact]
    public async Task La_legitimidad_se_evalua_en_la_fecha_evaluada_y_no_en_la_actual()
    {
        // Antes del cambio de compañía la misma persona sí era legítima: la re-derivación depende de
        // la fecha consultada, no de la situación de hoy.
        var escenario = await MontarConcedidoAsync(comoContratista: false);

        await CambiarPertenenciaSinCascadaAsync(escenario, escenario.PrincipalB.Id);

        // El día anterior a la misma hora local (09:00 en Lima), dentro del bloque horario y antes del
        // cambio de compañía.
        var resultadoPrevio = await escenario.EvaluarAsync(
            fechaHora: EscenarioPermisos.Instante.AddDays(-1));

        resultadoPrevio.MotivoDenegacion.Should().BeNull();
        resultadoPrevio.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        (await escenario.EvaluarAsync())
            .MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
    }

    // --- Montaje ----------------------------------------------------------------------------------

    private async Task<EscenarioPermisos> MontarConcedidoAsync(bool comoContratista)
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync(comoContratista);

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA, bloques: EscenarioPermisos.BloquesTodaLaSemana()));

        // Premisa: antes de la inconsistencia, la evaluación concede.
        (await escenario.EvaluarAsync()).Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        return escenario;
    }

    /// <summary>
    /// Cierra la pertenencia vigente y abre otra escribiendo en la base de datos, sin la cascada.
    /// </summary>
    /// <remarks>
    /// Dos <c>SaveChanges</c> y en ese orden: el trigger de no-solapamiento se evalúa por sentencia y
    /// rechazaría el INSERT si viera la pertenencia anterior todavía abierta.
    /// </remarks>
    private Task CambiarPertenenciaSinCascadaAsync(EscenarioPermisos escenario, Guid nuevaCompaniaId) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            var actual = await db.Set<AsignacionPersonaCompania>()
                .SingleAsync(a => a.PersonaId == escenario.Persona.Id);

            actual.FechaHoraFin = EscenarioPermisos.Instante.AddHours(-2);
            await db.SaveChangesAsync();

            db.Set<AsignacionPersonaCompania>().Add(new AsignacionPersonaCompania
            {
                PersonaId = escenario.Persona.Id,
                CompaniaId = nuevaCompaniaId,
                FechaHoraInicio = EscenarioPermisos.Instante.AddHours(-1),
                FechaHoraFin = EscenarioPermisos.Instante.AddYears(1),
            });

            await db.SaveChangesAsync();
        });

    /// <summary>La evaluación no cierra ni modifica el contexto: solo se niega a usarlo (research.md §7).</summary>
    private async Task ContextoSigueIntactoAsync(EscenarioPermisos escenario)
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var contexto = await db.Set<ContextoOperativoPersonaPrincipal>().AsNoTracking()
                .SingleAsync(c => c.Id == escenario.ContextoId);

            contexto.Estado.Should().Be(Estado.ACTIVO);
            contexto.MotivoFin.Should().BeNull();
            contexto.RevocadoPorPertenenciaId.Should().BeNull();
            contexto.FechaHoraFin.Should().BeAfter(EscenarioPermisos.Instante);
        });
    }
}
