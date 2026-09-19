using System.Net;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Reconstrucción del estado efectivo de una persona en una fecha dada (RF-037).
/// </summary>
/// <remarks>
/// Todo se deriva comparando las ventanas de vigencia contra la fecha evaluada, nunca a partir de un
/// campo de estado. Eso permite reconstruir correctamente también fechas pasadas, en las que los
/// campos <c>Estado</c> actuales ya no describen la situación de entonces.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class EstadoEfectivoTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Devuelve_la_compania_de_pertenencia_vigente_en_la_fecha()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var estado = await escenario.EstadoEfectivoAsync(DateTime.UtcNow);

        estado.PersonaId.Should().Be(escenario.Persona.Id);
        estado.CompaniaVigenteId.Should().Be(escenario.Contratista.Id);
    }

    [Fact]
    public async Task Devuelve_todos_los_contextos_vigentes_no_solo_uno()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contextoA = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var contextoB = await escenario.ContextoAsync(escenario.PrincipalB.Id);

        var estado = await escenario.EstadoEfectivoAsync(DateTime.UtcNow);

        // CS-013: una persona puede operar con varias Principales a la vez. Devolver solo una
        // ocultaría la mitad de su situación.
        estado.ContextosOperativosVigentes.Should().HaveCount(2);
        estado.ContextosOperativosVigentes.Select(c => c.ContextoOperativoId)
            .Should().BeEquivalentTo([contextoA.Id, contextoB.Id]);
    }

    [Fact]
    public async Task Cada_contexto_trae_su_propia_unidad_organizativa_vigente()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contextoA = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var contextoB = await escenario.ContextoAsync(escenario.PrincipalB.Id);

        var unidadA = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);

        using (var asignada = await escenario.AsignarUnidadAsync(contextoA.Id, unidadA))
        {
            asignada.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var estado = await escenario.EstadoEfectivoAsync(DateTime.UtcNow);

        var conUnidad = estado.ContextosOperativosVigentes
            .Single(c => c.ContextoOperativoId == contextoA.Id);

        conUnidad.UnidadOrganizativaVigenteId.Should().Be(unidadA);

        // El contexto sin unidad asignada la devuelve nula, no omite el contexto.
        var sinUnidad = estado.ContextosOperativosVigentes
            .Single(c => c.ContextoOperativoId == contextoB.Id);

        sinUnidad.UnidadOrganizativaVigenteId.Should().BeNull();
    }

    [Fact]
    public async Task Devuelve_los_perfiles_vigentes_en_la_fecha()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        using (var asignado = await escenario.AsignarPerfilAsync(tipo))
        {
            asignado.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var estado = await escenario.EstadoEfectivoAsync(DateTime.UtcNow);

        estado.PerfilesVigentesIds.Should().Contain(tipo);
    }

    [Fact]
    public async Task Una_fecha_anterior_a_la_pertenencia_devuelve_estado_vacio()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var estado = await escenario.EstadoEfectivoAsync(DateTime.UtcNow.AddYears(-5));

        // La persona existía, pero en esa fecha no pertenecía a ninguna compañía.
        estado.CompaniaVigenteId.Should().BeNull();
        estado.ContextosOperativosVigentes.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_fecha_posterior_al_fin_devuelve_estado_vacio()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var estado = await escenario.EstadoEfectivoAsync(DateTime.UtcNow.AddYears(5));

        estado.CompaniaVigenteId.Should().BeNull();
        estado.ContextosOperativosVigentes.Should().BeEmpty();
    }

    [Fact]
    public async Task El_estado_pasado_se_reconstruye_aunque_hoy_este_revocado()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var enPlenaVigencia = DateTime.UtcNow;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddDays(-1)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Hoy el contexto está INACTIVO y revocado; pero en la fecha consultada estaba vigente.
        // Derivar todo por fechas —y no por Estado— es lo que hace posible esta reconstrucción.
        var anterior = await escenario.EstadoEfectivoAsync(enPlenaVigencia.AddMonths(-1) > pertenencia.FechaHoraInicio
            ? enPlenaVigencia.AddMonths(-1)
            : pertenencia.FechaHoraInicio.AddDays(1));

        anterior.CompaniaVigenteId.Should().Be(escenario.Contratista.Id);
        anterior.ContextosOperativosVigentes.Select(c => c.ContextoOperativoId)
            .Should().Contain(contexto.Id);
    }

    [Fact]
    public async Task La_fecha_evaluada_se_devuelve_en_la_respuesta()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var fecha = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var estado = await escenario.EstadoEfectivoAsync(fecha);

        // Permite al cliente confirmar sobre qué instante se resolvió la consulta.
        estado.FechaHoraEvaluada.Should().Be(fecha);
    }
}
