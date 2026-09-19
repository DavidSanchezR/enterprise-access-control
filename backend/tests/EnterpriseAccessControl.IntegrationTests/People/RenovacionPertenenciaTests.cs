using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Renovación de pertenencia (RF-073): extiende la vigencia sin cerrar nada ni disparar la cascada.
/// </summary>
/// <remarks>
/// Es la contraparte inversa de <c>/finalizar</c>: esa operación acorta y cierra; ésta extiende sin
/// cerrar. Renovar solo amplía el techo temporal que RF-072 permitirá a las asociaciones creadas
/// <em>después</em>; las que ya existen no se tocan.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RenovacionPertenenciaTests(SqlServerFixture fixture)
{
    private Task<EscenarioUs5> MontarAsync() => new EscenarioUs5(fixture).MontarAsync();

    [Fact]
    public async Task Renovar_extiende_la_fecha_de_fin_sin_crear_una_nueva_pertenencia()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();
        var nuevoFin = DateTime.UtcNow.AddYears(2);

        using var respuesta = await escenario.RenovarAsync(pertenencia.Id, nuevoFin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pertenencias = await escenario.PertenenciasEnBaseAsync();

        // Sigue habiendo una sola fila: renovar no es crear una pertenencia nueva.
        pertenencias.Should().ContainSingle();
        pertenencias[0].FechaHoraFin.Should().Be(Vigencia.NormalizarFin(nuevoFin));
    }

    [Fact]
    public async Task Renovar_no_modifica_inicio_estado_ni_motivo()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();

        using (var respuesta = await escenario.RenovarAsync(
            pertenencia.Id, DateTime.UtcNow.AddYears(2)))
        {
            respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var renovada = (await escenario.PertenenciasEnBaseAsync())[0];

        renovada.FechaHoraInicio.Should().Be(pertenencia.FechaHoraInicio);
        renovada.Estado.Should().Be(EstadoPertenencia.ACTIVA);
        renovada.MotivoFin.Should().BeNull("renovar no cierra la pertenencia");
    }

    [Fact]
    public async Task Renovar_no_dispara_la_cascada_de_revocacion()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();
        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var unidadId = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);
        using (var asignada = await escenario.AsignarUnidadAsync(contexto.Id, unidadId))
        {
            asignada.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using (var renovada = await escenario.RenovarAsync(
            pertenencia.Id, DateTime.UtcNow.AddYears(2)))
        {
            renovada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Ningún dependiente cambia: no hay nada que revocar porque nada se cerró.
        (await escenario.ContextosEnBaseAsync()).Should()
            .OnlyContain(c => c.Estado == Estado.ACTIVO && c.RevocadoPorPertenenciaId == null);

        (await escenario.UnidadesEnBaseAsync()).Should()
            .OnlyContain(u => u.Estado == Estado.ACTIVO && u.RevocadoPorPertenenciaId == null);

        (await escenario.CredencialesEnBaseAsync()).Should()
            .OnlyContain(c => c.Estado == EstadoCredencial.ASIGNADO);
    }

    [Fact]
    public async Task Renovar_no_extiende_las_asociaciones_dependientes_ya_existentes()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();
        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var finOriginal = contexto.FechaHoraFin;

        using (var renovada = await escenario.RenovarAsync(
            pertenencia.Id, DateTime.UtcNow.AddYears(2)))
        {
            renovada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // La renovación amplía el techo para lo que se cree después, no alarga lo ya concedido.
        var contextos = await escenario.ContextosEnBaseAsync();
        contextos.Should().ContainSingle().Which.FechaHoraFin.Should().Be(finOriginal);
    }

    [Fact]
    public async Task Renovar_amplia_el_techo_para_asociaciones_creadas_despues()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();
        var masAlla = DateTime.UtcNow.AddMonths(18);

        // Antes de renovar, una asociación que exceda la pertenencia se rechaza (RF-072).
        using (var excedida = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, EscenarioUs5.InicioPertenencia, masAlla))
        {
            excedida.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        using (var renovada = await escenario.RenovarAsync(
            pertenencia.Id, DateTime.UtcNow.AddYears(2)))
        {
            renovada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Tras renovar, la misma petición cabe dentro de la nueva ventana.
        using var ahoraCabe = await escenario.AbrirContextoAsync(
            escenario.PrincipalA.Id, EscenarioUs5.InicioPertenencia, masAlla);

        ahoraCabe.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Una_fecha_no_estrictamente_posterior_se_rechaza()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();

        // Misma fecha: no es una renovación, es una operación sin efecto.
        using var misma = await escenario.RenovarAsync(pertenencia.Id, pertenencia.FechaHoraFin);

        misma.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await misma.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("RENOVACION_NO_POSTERIOR");
    }

    [Fact]
    public async Task Una_fecha_anterior_a_la_vigente_se_rechaza()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();

        // Acortar no es renovar: para eso existe /finalizar.
        using var anterior = await escenario.RenovarAsync(
            pertenencia.Id, DateTime.UtcNow.AddMonths(3));

        anterior.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Una_pertenencia_FINALIZADA_no_es_renovable()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var renovar = await escenario.RenovarAsync(
            pertenencia.Id, DateTime.UtcNow.AddYears(2));

        renovar.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await renovar.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("PERTENENCIA_NO_RENOVABLE");
    }

    [Fact]
    public async Task Una_pertenencia_ACTIVA_pero_ya_expirada_no_es_renovable()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // Sigue ACTIVA porque nadie la cerró, pero su vigencia ya pasó.
        var expirada = await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id,
            DateTime.UtcNow.AddMonths(-6),
            DateTime.UtcNow.AddMonths(-1));

        using var renovar = await escenario.RenovarAsync(
            expirada.Id, DateTime.UtcNow.AddYears(1));

        // RF-073: renovarla puentearía retroactivamente un vacío temporal ya transcurrido. Para
        // volver a vincular a la persona hace falta una pertenencia nueva.
        renovar.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await renovar.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("PERTENENCIA_NO_RENOVABLE");
    }

    [Fact]
    public async Task Omitir_la_nueva_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta($"historial-companias/{pertenencia.Id}/renovar"),
            new { },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task La_fecha_renovada_se_normaliza_al_fin_del_dia()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync();
        var nuevoFin = new DateTime(2028, 3, 15, 10, 22, 0, DateTimeKind.Utc);

        using (var respuesta = await escenario.RenovarAsync(pertenencia.Id, nuevoFin))
        {
            respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await escenario.PertenenciasEnBaseAsync())[0].FechaHoraFin
            .Should().Be(new DateTime(2028, 3, 15, 23, 59, 59, 999, DateTimeKind.Utc));
    }
}
