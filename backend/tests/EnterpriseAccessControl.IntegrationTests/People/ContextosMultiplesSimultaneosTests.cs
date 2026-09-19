using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Contextos operativos simultáneos con Principales distintas, sin límite superior
/// (RF-052, CS-013, CS-030).
/// </summary>
/// <remarks>
/// Es el escenario central del negocio: un trabajador de una contratista presta servicios a varias
/// mineras a la vez. RF-014 acota la cardinalidad de la <em>pertenencia</em> —una sola activa— y en
/// ningún momento la de los contextos, que solo gobierna RF-052.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContextosMultiplesSimultaneosTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Una_persona_opera_con_dos_principales_a_la_vez()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contextoA = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var contextoB = await escenario.ContextoAsync(escenario.PrincipalB.Id);

        // Mismo rango temporal, Principales distintas: el trigger se particiona por el par
        // (persona, Principal), no solo por persona.
        contextoA.FechaHoraInicio.Should().Be(contextoB.FechaHoraInicio);
        contextoA.FechaHoraFin.Should().Be(contextoB.FechaHoraFin);

        var contextos = await escenario.ContextosEnBaseAsync();
        contextos.Should().HaveCount(2);
        contextos.Should().OnlyContain(c => c.Estado == Estado.ACTIVO);
        contextos.Select(c => c.CompaniaPrincipalId)
            .Should().BeEquivalentTo([escenario.PrincipalA.Id, escenario.PrincipalB.Id]);
    }

    [Fact]
    public async Task No_existe_limite_superior_de_principales_simultaneas()
    {
        // CS-030: la especificación no fija tope. Se comprueba con cinco, más de las que cualquier
        // implementación con un límite implícito toleraría.
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var principales = new List<Guid> { escenario.PrincipalA.Id, escenario.PrincipalB.Id };

        for (var i = 0; i < 3; i++)
        {
            var extra = await fixture.Api.SembrarCompaniaAsync(
                $"Principal extra {EscenarioUs5.Sufijo()}");

            await escenario.SembrarRelacionAsync(escenario.Contratista.Id, extra.Id);
            principales.Add(extra.Id);
        }

        // El alcance del usuario debe cubrir también las nuevas Principales.
        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"multi.{Guid.CreateVersion7():N}@empresa.cl",
            EscenarioUs5.Password,
            alcanceCompanias: [escenario.Contratista.Id, .. principales]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        foreach (var principalId in principales)
        {
            using var respuesta = await cliente.PostAsJsonAsync(
                escenario.Ruta("contextos-operativos"),
                new ContextoOperativoRequest(
                    principalId, EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia),
                ApiFactory.Json);

            respuesta.StatusCode.Should().Be(
                HttpStatusCode.Created, "la Principal {0} debe admitirse", principalId);
        }

        (await escenario.ContextosEnBaseAsync()).Should().HaveCount(5);
    }

    [Fact]
    public async Task No_se_admiten_dos_contextos_solapados_con_la_misma_principal()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        using var duplicado = await escenario.AbrirContextoAsync(escenario.PrincipalA.Id);

        // Mismo inicio que el vigente: no hay forma de cerrarlo antes de que empiece el nuevo, así
        // que se rechaza con 409 desde la aplicación —no desde el trigger, que produciría un 500—.
        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Abrir_un_contexto_posterior_cierra_el_anterior_de_la_misma_principal()
    {
        // contracts/people.yaml: "Cierra automáticamente cualquier contexto activo previo para la
        // misma Compañía Principal". Es un reemplazo, no un conflicto.
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var primero = await escenario.ContextoAsync(escenario.PrincipalA.Id);

        using var segundo = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(1), EscenarioUs5.FinPertenencia);

        segundo.StatusCode.Should().Be(HttpStatusCode.Created);

        var contextos = await escenario.ContextosEnBaseAsync();
        contextos.Should().HaveCount(2);

        var cerrado = contextos.Single(c => c.Id == primero.Id);
        cerrado.Estado.Should().Be(Estado.INACTIVO);
        cerrado.MotivoFin.Should().Be(MotivoFinRevocacion.REEMPLAZO_ASIGNACION);

        // Un reemplazo no es una revocación en cascada: no se atribuye a ninguna pertenencia.
        cerrado.RevocadoPorPertenenciaId.Should().BeNull();
    }

    [Fact]
    public async Task Cada_contexto_puede_tener_su_propia_unidad_organizativa()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contextoA = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var contextoB = await escenario.ContextoAsync(escenario.PrincipalB.Id);

        var unidadA = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id, "Planta A");
        var unidadB = await escenario.SembrarUnidadAsync(escenario.PrincipalB.Id, "Planta B");

        using (var a = await escenario.AsignarUnidadAsync(contextoA.Id, unidadA))
        {
            a.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using (var b = await escenario.AsignarUnidadAsync(contextoB.Id, unidadB))
        {
            // CS-014: la exclusividad de unidad es por contexto, no por persona.
            b.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var unidades = await escenario.UnidadesEnBaseAsync();
        unidades.Should().HaveCount(2);
        unidades.Select(u => u.UnidadOrganizativaId).Should().BeEquivalentTo([unidadA, unidadB]);
    }

    [Fact]
    public async Task Cada_contexto_puede_tener_su_propia_credencial()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        await escenario.ContextoAsync(escenario.PrincipalA.Id);
        await escenario.ContextoAsync(escenario.PrincipalB.Id);

        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);
        await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        // CS-016/CS-017: credenciales ASIGNADO simultáneas para Principales distintas.
        var credenciales = await escenario.CredencialesEnBaseAsync();
        credenciales.Should().HaveCount(2);
        credenciales.Should().OnlyContain(c => c.Estado == EstadoCredencial.ASIGNADO);
    }

    [Fact]
    public async Task El_listado_devuelve_todos_los_contextos_de_la_persona()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);
        await escenario.ContextoAsync(escenario.PrincipalB.Id);

        var contextos = await escenario.Cliente
            .GetFromJsonAsync<IReadOnlyList<ContextoOperativoDto>>(
                escenario.Ruta("contextos-operativos"), ApiFactory.Json);

        contextos.Should().HaveCount(2);
    }
}
