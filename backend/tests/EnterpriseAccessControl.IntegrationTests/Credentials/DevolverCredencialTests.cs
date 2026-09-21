using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.AreaAccess;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.Credentials;

/// <summary>
/// Devolución de una credencial: DEVUELTO y cierre de la vigencia (Historia 9; contracts/credentials.yaml).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class DevolverCredencialTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Devolver_una_credencial_asignada_la_marca_DEVUELTO_y_cierra_su_vigencia()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        credencial.FechaHoraFin.Should().BeAfter(DateTime.UtcNow, "premisa: la credencial seguía vigente");

        var antes = DateTime.UtcNow.AddSeconds(-1);

        using var respuesta = await escenario.DevolverAsync(credencial.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var devuelta = await escenario.LeerCredencialAsync(credencial.Id);

        devuelta.Estado.Should().Be(EstadoCredencial.DEVUELTO);

        // data-model.md: el fin se ajusta al valor efectivo de cierre, sin extender lo declarado.
        devuelta.FechaHoraFin.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
        devuelta.EstaVigenteEn(DateTime.UtcNow.AddSeconds(1)).Should().BeFalse();

        // RF-063: el resto del registro no cambia.
        devuelta.FechaHoraInicio.Should().Be(credencial.FechaHoraInicio);
        devuelta.CompaniaPrincipalId.Should().Be(credencial.CompaniaPrincipalId);
        devuelta.TipoCredencialId.Should().Be(credencial.TipoCredencialId);
        devuelta.RevocadoPorPertenenciaId.Should().BeNull("devolver no es la cascada de revocación");
    }

    [Fact]
    public async Task Devolver_una_credencial_no_afecta_a_la_de_otra_principal()
    {
        // Independent Test de la Historia 9: credenciales simultáneas para dos Principales.
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var deA = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);
        var deB = await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        using (var _ = await escenario.DevolverAsync(deA.Id))
        {
        }

        var intacta = await escenario.LeerCredencialAsync(deB.Id);

        intacta.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        intacta.FechaHoraFin.Should().Be(deB.FechaHoraFin);
    }

    [Fact]
    public async Task Devolver_dos_veces_se_rechaza_con_409()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using (var primera = await escenario.DevolverAsync(credencial.Id))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var finTrasDevolver = (await escenario.LeerCredencialAsync(credencial.Id)).FechaHoraFin;

        using var segunda = await escenario.DevolverAsync(credencial.Id);

        await EsperarProblemaAsync(segunda, HttpStatusCode.Conflict, CodigosError.CredencialNoAsignada);

        // El rechazo no vuelve a tocar la fecha de cierre.
        (await escenario.LeerCredencialAsync(credencial.Id)).FechaHoraFin.Should().Be(finTrasDevolver);
    }

    [Theory]
    [InlineData(EstadoCredencial.ELIMINADO)]
    [InlineData(EstadoCredencial.REVOCADA)]
    public async Task No_se_puede_devolver_una_credencial_en_estado_terminal(EstadoCredencial estado)
    {
        // contracts/credentials.yaml: ASIGNADO es el único estado no terminal.
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        await escenario.FijarEstadoAsync(credencial.Id, estado);

        using var respuesta = await escenario.DevolverAsync(credencial.Id);

        await EsperarProblemaAsync(respuesta, HttpStatusCode.Conflict, CodigosError.CredencialNoAsignada);
        (await escenario.LeerCredencialAsync(credencial.Id)).Estado.Should().Be(estado);
    }

    [Fact]
    public async Task Tras_devolver_puede_emitirse_otra_credencial_para_la_misma_principal()
    {
        // El trigger de RF-057 solo compara filas ASIGNADO: la devuelta ya no reserva su ventana.
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var original = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using (var _ = await escenario.DevolverAsync(original.Id))
        {
        }

        using var reemplazo = await escenario.PostCredencialAsync(escenario.PrincipalA.Id);

        reemplazo.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Una_credencial_inexistente_o_de_otra_persona_devuelve_404()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var otra = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var ajena = await otra.AsignarCredencialAsync(otra.PrincipalA.Id);

        using (var inexistente = await escenario.DevolverAsync(Guid.CreateVersion7()))
        {
            inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // Un usuario que ve a ambas personas y administra ambas Principales A: el 404 solo puede venir
        // de que la credencial no pertenece a la persona de la ruta.
        using var ambos = await fixture.Api.ClienteDeAreasAsync(
            escenario.Contratista.Id, escenario.PrincipalA.Id, otra.Contratista.Id, otra.PrincipalA.Id);

        // Ruta de una persona con el identificador de la credencial de otra.
        using var cruzada = await escenario.DevolverAsync(ajena.Id, cliente: ambos);

        cruzada.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otra.LeerCredencialAsync(ajena.Id)).Estado.Should().Be(EstadoCredencial.ASIGNADO);

        // Contrapunto: el mismo usuario sí puede devolverla por la ruta correcta.
        using var correcta = await otra.DevolverAsync(ajena.Id, cliente: ambos);
        correcta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Sin_alcance_sobre_la_principal_de_la_credencial_devuelve_404()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        // Ve a la persona (tiene la Contratista) pero no administra la Principal A.
        using var parcial = await fixture.Api.ClienteDeAreasAsync(
            escenario.Contratista.Id, escenario.PrincipalB.Id);

        using var respuesta = await escenario.DevolverAsync(credencial.Id, parcial);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await escenario.LeerCredencialAsync(credencial.Id)).Estado.Should().Be(EstadoCredencial.ASIGNADO);
    }

    [Fact]
    public async Task Sin_alcance_sobre_la_persona_devuelve_404()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        // Desde D3 el alcance sobre una Persona es la UNIÓN de su pertenencia vigente y de sus
        // contextos operativos vigentes (RF-077), así que la Principal A sí la alcanza. Quien no la
        // alcanza es una compañía ajena a esa unión.
        var ajena = await fixture.Api.SembrarCompaniaAsync("Principal Sin Relación");
        using var ajeno = await fixture.Api.ClienteDeAreasAsync(ajena.Id);

        using var respuesta = await escenario.DevolverAsync(credencial.Id, ajeno);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task EsperarProblemaAsync(
        HttpResponseMessage respuesta,
        HttpStatusCode estado,
        string codigo)
    {
        respuesta.StatusCode.Should().Be(estado);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(codigo);
    }
}
