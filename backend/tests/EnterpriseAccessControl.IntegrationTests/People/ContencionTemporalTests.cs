using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Contención temporal jerárquica (RF-072, CS-034) sobre las tres asociaciones dependientes.
/// </summary>
/// <remarks>
/// Una asociación que depende de la pertenencia no puede sobrevivirla ni precederla. La igualdad
/// exacta en cualquiera de los dos extremos es válida: que una asociación dure exactamente lo mismo
/// que la pertenencia es el caso normal, no un error.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContencionTemporalTests(SqlServerFixture fixture)
{
    private async Task<EscenarioUs5> MontarConPertenenciaFijaAsync()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        // La pertenencia debe estar vigente *ahora* para servir de ancla, pero con bordes
        // conocidos: se usa una ventana amplia alrededor del presente.
        await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id,
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow.AddYears(1));

        return escenario;
    }

    private static async Task<(DateTime Inicio, DateTime Fin)> VentanaPertenenciaAsync(
        EscenarioUs5 escenario)
    {
        var pertenencia = (await escenario.PertenenciasEnBaseAsync())[0];
        return (pertenencia.FechaHoraInicio, pertenencia.FechaHoraFin);
    }

    [Fact]
    public async Task Un_contexto_con_fechas_identicas_a_la_pertenencia_se_acepta()
    {
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        var (desde, hasta) = await VentanaPertenenciaAsync(escenario);

        // CS-034: la igualdad exacta en ambos extremos es el caso más común.
        using var respuesta = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, desde, hasta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Un_contexto_que_empieza_antes_que_la_pertenencia_se_rechaza()
    {
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        var (desde, hasta) = await VentanaPertenenciaAsync(escenario);

        using var respuesta = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, desde.AddDays(-1), hasta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString()
            .Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task Un_contexto_que_termina_despues_que_la_pertenencia_se_rechaza()
    {
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        var (desde, hasta) = await VentanaPertenenciaAsync(escenario);

        // Es el caso que la regla existe para impedir: la asociación sobreviviría a la pertenencia
        // que la justifica.
        using var respuesta = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, desde, hasta.AddDays(1));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString()
            .Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task Una_asignacion_de_unidad_fuera_de_la_contencion_se_rechaza()
    {
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var unidad = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);

        var (_, hasta) = await VentanaPertenenciaAsync(escenario);

        using var respuesta = await escenario.AsignarUnidadAsync(
            contexto.Id, unidad, EscenarioUs5.InicioPertenencia, hasta.AddMonths(1));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString()
            .Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task Una_credencial_fuera_de_la_contencion_se_rechaza()
    {
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var (_, hasta) = await VentanaPertenenciaAsync(escenario);

        using var respuesta = await escenario.PostCredencialAsync(
            escenario.PrincipalA.Id, EscenarioUs5.InicioPertenencia, hasta.AddMonths(1));

        // RF-072 alcanza también a la credencial: es la tercera dependiente de la pertenencia.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString()
            .Should().Be(CodigosError.FueraDeContencionTemporal);
    }

    [Fact]
    public async Task Una_asignacion_estrictamente_interior_se_acepta()
    {
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        var (desde, hasta) = await VentanaPertenenciaAsync(escenario);

        using var respuesta = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, desde.AddDays(10), hasta.AddDays(-10));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task El_perfil_NO_esta_sujeto_a_contencion_temporal()
    {
        // RF-072 no le aplica: el modelo de dominio no declara AsignaciónTipoPersona dependiente de
        // la pertenencia (RF-011, perfiles múltiples sin exclusividad). Aplicarle la contención
        // sería inventar una dependencia que nadie estableció.
        var escenario = await MontarConPertenenciaFijaAsync();
        using var _ = escenario.Cliente;

        var tipoPersonaId = await SembrarTipoPersonaAsync(escenario);

        var (_, hasta) = await VentanaPertenenciaAsync(escenario);

        using var respuesta = await escenario.AsignarPerfilAsync(
            tipoPersonaId, EscenarioUs5.InicioPertenencia, hasta.AddYears(1));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    internal static async Task<Guid> SembrarTipoPersonaAsync(EscenarioUs5 escenario)
    {
        var id = Guid.Empty;

        await escenario.ConDatosAsync(async db =>
        {
            var tipo = new Domain.Entities.TipoPersona
            {
                Nombre = $"Perfil {EscenarioUs5.Sufijo()}",
                Estado = Domain.Enums.Estado.ACTIVO,
            };

            db.Set<Domain.Entities.TipoPersona>().Add(tipo);
            await db.SaveChangesAsync();
            id = tipo.Id;
        });

        return id;
    }
}
