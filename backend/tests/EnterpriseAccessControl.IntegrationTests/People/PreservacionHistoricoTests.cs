using System.Net;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Preservación del histórico durante la revocación en cascada (CS-026, RF-063).
/// </summary>
/// <remarks>
/// Revocar es cerrar, nunca borrar. Un registro revocado debe seguir diciendo cuándo empezó, qué
/// pasó y por causa de qué — es lo que permite auditar después "qué perdió esta persona y por qué".
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PreservacionHistoricoTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task La_revocacion_no_elimina_ningun_registro()
    {
        var (escenario, pertenencia, dependientes) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await escenario.ContextosEnBaseAsync()).Should().HaveCount(dependientes.Count);
        (await escenario.UnidadesEnBaseAsync()).Should().HaveCount(dependientes.Count);
        (await escenario.CredencialesEnBaseAsync()).Should().HaveCount(dependientes.Count);
        (await escenario.PertenenciasEnBaseAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task La_fecha_de_inicio_nunca_se_modifica()
    {
        var (escenario, pertenencia, dependientes) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        var iniciosOriginales = dependientes.ToDictionary(
            d => d.Contexto.Id, d => d.Contexto.FechaHoraInicio);

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        foreach (var contexto in await escenario.ContextosEnBaseAsync())
        {
            contexto.FechaHoraInicio.Should().Be(
                iniciosOriginales[contexto.Id],
                "el histórico debe seguir diciendo cuándo empezó (RF-063)");
        }
    }

    [Fact]
    public async Task Las_tres_entidades_quedan_con_su_disposicion_final_consultable()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Contexto y unidad llevan Estado + MotivoFin + origen.
        foreach (var contexto in await escenario.ContextosEnBaseAsync())
        {
            contexto.Estado.Should().Be(Estado.INACTIVO);
            contexto.MotivoFin.Should().Be(MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA);
            contexto.RevocadoPorPertenenciaId.Should().Be(pertenencia.Id);
        }

        foreach (var unidad in await escenario.UnidadesEnBaseAsync())
        {
            unidad.Estado.Should().Be(Estado.INACTIVO);
            unidad.MotivoFin.Should().Be(MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA);
            unidad.RevocadoPorPertenenciaId.Should().Be(pertenencia.Id);
        }

        // La credencial usa su propio estado REVOCADA en lugar de un MotivoFin paralelo.
        foreach (var credencial in await escenario.CredencialesEnBaseAsync())
        {
            credencial.Estado.Should().Be(EstadoCredencial.REVOCADA);
            credencial.RevocadoPorPertenenciaId.Should().Be(pertenencia.Id);
        }
    }

    [Fact]
    public async Task La_auditoria_puede_responder_que_revoco_esta_pertenencia()
    {
        var (escenario, pertenencia, dependientes) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Es la pregunta que RevocadoPorPertenenciaId existe para responder sin heurísticas de
        // fecha ni de compañía (research.md §14.2).
        var afectados = (await escenario.ContextosEnBaseAsync())
            .Count(c => c.RevocadoPorPertenenciaId == pertenencia.Id);

        afectados.Should().Be(dependientes.Count);
    }

    [Fact]
    public async Task La_auditoria_registra_cuando_y_quien_provoco_la_revocacion()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        var antesDelCese = DateTime.UtcNow.AddSeconds(-5);

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var contexto = (await escenario.ContextosEnBaseAsync())[0];

        // "Cuándo ocurrió" lo responde UpdatedAt y "quién lo causó" UpdatedById, ambos estampados
        // por el interceptor de auditoría: no hizo falta una tabla de eventos aparte.
        contexto.UpdatedAt.Should().BeAfter(antesDelCese);
        contexto.UpdatedById.Should().NotBeNull();
    }

    [Fact]
    public async Task Un_dependiente_que_ya_habia_terminado_conserva_su_fecha_original()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _c = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        // Contexto que termina antes que el cese que vendrá después.
        var contexto = await escenario.ContextoAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(3));

        var finOriginal = contexto.FechaHoraFin;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddMonths(6)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var despues = (await escenario.ContextosEnBaseAsync())[0];

        // La cascada solo acorta: extenderlo hasta la fecha del cese concedería una vigencia que
        // nunca se otorgó.
        despues.FechaHoraFin.Should().Be(finOriginal);
    }
}
