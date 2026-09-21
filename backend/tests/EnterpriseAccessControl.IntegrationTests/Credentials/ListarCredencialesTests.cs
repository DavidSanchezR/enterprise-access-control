using System.Net;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.AreaAccess;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.Credentials;

/// <summary>
/// Histórico de credenciales de una persona (Historia 9; RF-018; contracts/credentials.yaml).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ListarCredencialesTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task El_historico_se_ordena_por_fecha_de_inicio_descendente()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        var antigua = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-10));
        var deB = await escenario.AsignarCredencialAsync(
            escenario.PrincipalB.Id, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddMonths(3));
        var reciente = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(3));

        var historico = await escenario.ListarCredencialesAsync();

        // Varias Principales en el mismo histórico, del inicio más reciente al más antiguo.
        historico.Select(c => c.Id).Should().Equal(reciente.Id, deB.Id, antigua.Id);
        historico.Select(c => c.FechaHoraInicio).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task El_historico_puede_filtrarse_por_compania_principal()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        var deA = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);
        await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        var soloA = await escenario.ListarCredencialesAsync(escenario.PrincipalA.Id);

        soloA.Select(c => c.Id).Should().Equal(deA.Id);
        soloA.Should().OnlyContain(c => c.CompaniaPrincipalId == escenario.PrincipalA.Id);
    }

    [Fact]
    public async Task El_historico_incluye_credenciales_en_cualquier_estado()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        var devuelta = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-10));
        var eliminada = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(-9), DateTime.UtcNow.AddDays(-5));
        var vigente = await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        await escenario.FijarEstadoAsync(devuelta.Id, EstadoCredencial.DEVUELTO);
        await escenario.FijarEstadoAsync(eliminada.Id, EstadoCredencial.ELIMINADO);

        var historico = await escenario.ListarCredencialesAsync();

        historico.Select(c => (c.Id, c.Estado)).Should().BeEquivalentTo(
        [
            (devuelta.Id, EstadoCredencial.DEVUELTO),
            (eliminada.Id, EstadoCredencial.ELIMINADO),
            (vigente.Id, EstadoCredencial.ASIGNADO),
        ]);
    }

    [Fact]
    public async Task Una_persona_sin_credenciales_devuelve_una_lista_vacia()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        (await escenario.ListarCredencialesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Tras_cesar_la_pertenencia_el_historico_sigue_consultable_con_la_credencial_revocada()
    {
        // RF-037: el histórico de una persona sin pertenencia vigente sigue siendo accesible, y la
        // cascada de RF-061 deja la credencial en REVOCADA con su pertenencia de origen.
        var escenario = await new People.EscenarioUs5(fixture).MontarAsync();
        var pertenencia = await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var historico = await escenario.ListarCredencialesAsync();

        var revocada = historico.Should().ContainSingle().Which;
        revocada.Id.Should().Be(credencial.Id);
        revocada.Estado.Should().Be(EstadoCredencial.REVOCADA);
        revocada.RevocadoPorPertenenciaId.Should().Be(pertenencia.Id);
    }

    [Fact]
    public async Task Sin_alcance_sobre_la_persona_el_historico_devuelve_404()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        // Desde D3 el alcance sobre una Persona es la UNIÓN de su pertenencia vigente y de sus
        // contextos operativos vigentes (RF-077): la Principal A ya está DENTRO del alcance porque la
        // persona tiene contexto con ella. Para probar la ausencia de alcance hace falta una compañía
        // ajena a esa unión.
        var ajena = await fixture.Api.SembrarCompaniaAsync("Principal Sin Relación");
        using var ajeno = await fixture.Api.ClienteDeAreasAsync(ajena.Id);

        using var respuesta = await ajeno.GetAsync(escenario.Ruta("credenciales"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
