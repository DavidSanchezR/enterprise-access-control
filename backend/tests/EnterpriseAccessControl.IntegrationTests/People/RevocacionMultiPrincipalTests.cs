using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Ejemplo 2 de negocio (CS-027): una persona con accesos a dos Principales vía una única
/// Contratista los pierde **ambos** al terminar esa única pertenencia.
/// </summary>
/// <remarks>
/// Es el escenario que research.md §14.4 aclara expresamente: la cascada alcanza a todos los
/// contextos abiertos —sean uno o varios— precisamente porque todos dependen de la misma pertenencia
/// que RF-014 permite tener activa. Que sean varios no los hace independientes.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RevocacionMultiPrincipalTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Terminar_la_unica_pertenencia_revoca_los_accesos_a_ambas_principales()
    {
        var (escenario, pertenencia, dependientes) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        dependientes.Should().HaveCount(2, "el escenario monta contextos hacia dos Principales");

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var contextos = await escenario.ContextosEnBaseAsync();

        // Ambas Principales quedan revocadas por la misma pertenencia.
        contextos.Should().HaveCount(2);
        contextos.Should().OnlyContain(c => c.RevocadoPorPertenenciaId == pertenencia.Id);

        contextos.Select(c => c.CompaniaPrincipalId)
            .Should().BeEquivalentTo([escenario.PrincipalA.Id, escenario.PrincipalB.Id]);
    }

    [Fact]
    public async Task Las_credenciales_de_ambas_principales_se_revocan_juntas()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var credenciales = await escenario.CredencialesEnBaseAsync();

        credenciales.Should().HaveCount(2);
        credenciales.Should().OnlyContain(c => c.Estado == EstadoCredencial.REVOCADA);
        credenciales.Select(c => c.CompaniaPrincipalId)
            .Should().BeEquivalentTo([escenario.PrincipalA.Id, escenario.PrincipalB.Id]);
    }

    [Fact]
    public async Task Las_unidades_de_ambos_contextos_se_revocan_juntas()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var unidades = await escenario.UnidadesEnBaseAsync();

        unidades.Should().HaveCount(2);
        unidades.Should().OnlyContain(u =>
            u.Estado == Estado.INACTIVO && u.RevocadoPorPertenenciaId == pertenencia.Id);
    }

    [Fact]
    public async Task Cinco_principales_simultaneas_se_revocan_todas()
    {
        // La cascada no depende de cuántos contextos haya: RF-052 no fija tope y CS-030 lo confirma.
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _c = escenario.Cliente;

        var principales = new List<Guid> { escenario.PrincipalA.Id, escenario.PrincipalB.Id };

        for (var i = 0; i < 3; i++)
        {
            var extra = await fixture.Api.SembrarCompaniaAsync(
                $"Principal extra {EscenarioUs5.Sufijo()}");

            await escenario.SembrarRelacionAsync(escenario.Contratista.Id, extra.Id);
            principales.Add(extra.Id);
        }

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"multirevoca.{Guid.CreateVersion7():N}@empresa.cl",
            EscenarioUs5.Password,
            alcanceCompanias: [escenario.Contratista.Id, .. principales]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        // Las peticiones van por el cliente de alcance ampliado: el original no cubre las tres
        // Principales añadidas y devolvería 404.
        var pertenencia = await CrearPertenenciaAsync(cliente, escenario);

        foreach (var principalId in principales)
        {
            using var abierto = await cliente.PostAsJsonAsync(
                escenario.Ruta("contextos-operativos"),
                new ContextoOperativoRequest(
                    principalId, EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia),
                ApiFactory.Json);

            abierto.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using (var cese = await cliente.PostAsJsonAsync(
            escenario.Ruta($"historial-companias/{pertenencia.Id}/finalizar"),
            new FinalizarPertenenciaRequest(DateTime.UtcNow),
            ApiFactory.Json))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var contextos = await escenario.ContextosEnBaseAsync();

        contextos.Should().HaveCount(5);
        contextos.Should().OnlyContain(c =>
            c.Estado == Estado.INACTIVO && c.RevocadoPorPertenenciaId == pertenencia.Id);
    }

    private static async Task<AsignacionCompaniaDto> CrearPertenenciaAsync(
        HttpClient cliente,
        EscenarioUs5 escenario)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            escenario.Ruta("historial-companias"),
            new AsignacionCompaniaRequest(
                escenario.Contratista.Id,
                EscenarioUs5.InicioPertenencia,
                EscenarioUs5.FinPertenencia),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AsignacionCompaniaDto>(ApiFactory.Json))!;
    }
}
