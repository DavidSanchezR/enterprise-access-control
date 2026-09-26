using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// CS-042 sobre el perfil (<c>AsignaciónTipoPersona</c>): contención temporal de RF-082.
/// </summary>
/// <remarks>
/// Cambio de requisito post-Baseline VF-007. El perfil queda contenido en la pertenencia vigente de la
/// persona igual que las tres asociaciones de RF-072 —la igualdad en ambos extremos es válida—, pero
/// sin volverse dependiente de ella a efectos de la cascada. Se verifica el <c>codigo</c> del
/// <c>ProblemDetails</c> y no solo el estado HTTP: <c>400</c> y <c>409</c> tienen otras causas en esta
/// misma operación.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContencionPerfilesTests(SqlServerFixture fixture)
{
    private async Task<(EscenarioUs5 Escenario, Guid TipoPersonaId, DateTime Desde, DateTime Hasta)>
        MontarConPertenenciaAsync()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddYears(1));

        // Los bordes se leen de la base: la pertenencia se normaliza a días completos al crearse.
        var pertenencia = (await escenario.PertenenciasEnBaseAsync())[0];
        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        return (escenario, tipo, pertenencia.FechaHoraInicio, pertenencia.FechaHoraFin);
    }

    [Fact]
    public async Task CS042_a_un_perfil_que_termina_antes_que_la_pertenencia_se_acepta()
    {
        var (escenario, tipo, desde, hasta) = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.AsignarPerfilAsync(tipo, desde, hasta.AddDays(-30));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CS042_b_un_perfil_con_las_mismas_fechas_que_la_pertenencia_se_acepta()
    {
        var (escenario, tipo, desde, hasta) = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        // La igualdad exacta en ambos extremos es el caso normal, no un error.
        using var respuesta = await escenario.AsignarPerfilAsync(tipo, desde, hasta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CS042_c_un_perfil_que_termina_despues_que_la_pertenencia_se_rechaza()
    {
        var (escenario, tipo, desde, hasta) = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.AsignarPerfilAsync(tipo, desde, hasta.AddDays(1));

        await VerificarProblemaAsync(
            respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task CS042_d_un_perfil_que_empieza_antes_que_la_pertenencia_se_rechaza()
    {
        var (escenario, tipo, desde, hasta) = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.AsignarPerfilAsync(tipo, desde.AddDays(-1), hasta);

        await VerificarProblemaAsync(
            respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task CS042_e_un_perfil_sin_pertenencia_vigente_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        // Sin pertenencia no hay límite dentro del cual contener el perfil (RF-082).
        using var respuesta = await escenario.AsignarPerfilAsync(tipo);

        await VerificarProblemaAsync(
            respuesta, HttpStatusCode.BadRequest, CodigosError.SinPertenenciaVigente);
    }

    [Fact]
    public async Task Una_pertenencia_activa_pero_ya_vencida_no_sirve_de_limite()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        // Estado ACTIVA con la fecha de fin ya pasada: la vigencia se decide por fechas, nunca por
        // Estado (Principio IV), así que la persona no tiene pertenencia vigente.
        await escenario.ConDatosAsync(async db =>
        {
            db.Set<AsignacionPersonaCompania>().Add(new AsignacionPersonaCompania
            {
                PersonaId = escenario.Persona.Id,
                CompaniaId = escenario.Contratista.Id,
                FechaHoraInicio = DateTime.UtcNow.AddMonths(-3),
                FechaHoraFin = DateTime.UtcNow.AddDays(-1),
            });

            await db.SaveChangesAsync();
        });

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        using var respuesta = await escenario.AsignarPerfilAsync(
            tipo, DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(1));

        await VerificarProblemaAsync(
            respuesta, HttpStatusCode.BadRequest, CodigosError.SinPertenenciaVigente);
    }

    [Fact]
    public async Task Varios_perfiles_simultaneos_dentro_de_la_pertenencia_se_aceptan()
    {
        var (escenario, trabajador, desde, hasta) = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        var visitante = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        // RF-082 no cambia RF-011: la contención no introduce exclusividad entre perfiles.
        foreach (var tipo in new[] { trabajador, visitante })
        {
            using var respuesta = await escenario.AsignarPerfilAsync(tipo, desde, hasta);
            respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    internal static async Task VerificarProblemaAsync(
        HttpResponseMessage respuesta,
        HttpStatusCode estado,
        string codigo)
    {
        respuesta.StatusCode.Should().Be(estado);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(codigo);
    }
}
