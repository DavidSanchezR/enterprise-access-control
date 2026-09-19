using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Credentials;

/// <summary>
/// Baja lógica de una credencial: ELIMINADO, sin borrar el histórico (Historia 9; RF-018).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class EliminarCredencialTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Delete_aplica_baja_logica_y_conserva_el_registro()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using var respuesta = await escenario.EliminarAsync(credencial.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // La fila sigue existiendo: nunca hay eliminación física del histórico.
        var eliminada = await escenario.LeerCredencialAsync(credencial.Id);

        eliminada.Estado.Should().Be(EstadoCredencial.ELIMINADO);
        eliminada.PersonaId.Should().Be(credencial.PersonaId);
        eliminada.CompaniaPrincipalId.Should().Be(credencial.CompaniaPrincipalId);
        eliminada.TipoCredencialId.Should().Be(credencial.TipoCredencialId);
        eliminada.FechaHoraInicio.Should().Be(credencial.FechaHoraInicio);
        eliminada.RevocadoPorPertenenciaId.Should().BeNull();

        // data-model.md: al cerrarse administrativamente, el fin se ajusta al cierre efectivo.
        eliminada.FechaHoraFin.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
        eliminada.FechaHoraFin.Should().BeBefore(credencial.FechaHoraFin);
    }

    [Fact]
    public async Task La_credencial_eliminada_sigue_apareciendo_en_el_historico()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using (var _ = await escenario.EliminarAsync(credencial.Id))
        {
        }

        var historico = await escenario.ListarCredencialesAsync();

        historico.Should().ContainSingle(c => c.Id == credencial.Id)
            .Which.Estado.Should().Be(EstadoCredencial.ELIMINADO);
    }

    [Fact]
    public async Task Eliminar_no_borra_filas_de_la_tabla()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var deA = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);
        await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        using (var _ = await escenario.EliminarAsync(deA.Id))
        {
        }

        var filas = 0;
        await escenario.ConDatosAsync(async db =>
            filas = await db.Set<AsignacionCredencial>().CountAsync(c => c.PersonaId == escenario.Persona.Id));

        filas.Should().Be(2);
    }

    [Fact]
    public async Task Eliminar_una_credencial_no_afecta_a_la_de_otra_principal()
    {
        // Independent Test de la Historia 9: una se devuelve, la otra se da de baja.
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var deA = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);
        var deB = await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        using (var devuelta = await escenario.DevolverAsync(deA.Id))
        {
            devuelta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using (var eliminada = await escenario.EliminarAsync(deB.Id))
        {
            eliminada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await escenario.LeerCredencialAsync(deA.Id)).Estado.Should().Be(EstadoCredencial.DEVUELTO);
        (await escenario.LeerCredencialAsync(deB.Id)).Estado.Should().Be(EstadoCredencial.ELIMINADO);
    }

    [Theory]
    [InlineData(EstadoCredencial.DEVUELTO)]
    [InlineData(EstadoCredencial.REVOCADA)]
    [InlineData(EstadoCredencial.ELIMINADO)]
    public async Task No_se_puede_dar_de_baja_una_credencial_en_estado_terminal(EstadoCredencial estado)
    {
        // contracts/credentials.yaml: ASIGNADO es el único estado no terminal. Convertir una REVOCADA
        // en ELIMINADO borraría la huella de la cascada que la revocó.
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        await escenario.FijarEstadoAsync(credencial.Id, estado);

        using var respuesta = await escenario.EliminarAsync(credencial.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(CodigosError.CredencialNoAsignada);

        (await escenario.LeerCredencialAsync(credencial.Id)).Estado.Should().Be(estado);
    }

    [Fact]
    public async Task Eliminar_una_credencial_inexistente_devuelve_404()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        using var respuesta = await escenario.EliminarAsync(Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
