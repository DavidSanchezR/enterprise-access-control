using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Cese explícito de pertenencia (RF-061): misma cascada que el reemplazo, pero sin que otra
/// compañía la sustituya.
/// </summary>
/// <remarks>
/// Es el caso de la desvinculación: la persona deja de pertenecer a la compañía y no entra en otra.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class CeseExplicitoPertenenciaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task El_cese_revoca_las_tres_entidades_dependientes()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await escenario.ContextosEnBaseAsync()).Should().OnlyContain(c =>
            c.Estado == Estado.INACTIVO
            && c.MotivoFin == MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA
            && c.RevocadoPorPertenenciaId == pertenencia.Id);

        (await escenario.UnidadesEnBaseAsync()).Should().OnlyContain(u =>
            u.Estado == Estado.INACTIVO
            && u.RevocadoPorPertenenciaId == pertenencia.Id);

        (await escenario.CredencialesEnBaseAsync()).Should().OnlyContain(c =>
            c.Estado == EstadoCredencial.REVOCADA
            && c.RevocadoPorPertenenciaId == pertenencia.Id);
    }

    [Fact]
    public async Task El_cese_no_crea_una_nueva_pertenencia()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // A diferencia del reemplazo, aquí la persona queda sin compañía.
        var pertenencias = await escenario.PertenenciasEnBaseAsync();
        pertenencias.Should().ContainSingle();
        pertenencias[0].MotivoFin.Should().Be(MotivoFinPertenencia.CESE_PERTENENCIA);
    }

    [Fact]
    public async Task Un_segundo_cese_sobre_la_misma_pertenencia_se_rechaza()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var primero = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            primero.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var segundo = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow);

        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Un_cese_anterior_al_inicio_de_la_pertenencia_se_rechaza()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using var respuesta = await escenario.FinalizarAsync(
            pertenencia.Id, pertenencia.FechaHoraInicio.AddDays(-10));

        // RF-039: un intervalo invertido no es una vigencia válida.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Omitir_la_fecha_de_cese_se_rechaza_con_400()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta($"historial-companias/{pertenencia.Id}/finalizar"),
            new { },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tras_el_cese_no_se_puede_abrir_un_contexto_nuevo()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow.AddDays(-1)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        // Sin pertenencia vigente desaparece el ancla que sustenta cualquier contexto.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
