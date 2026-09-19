using System.Net;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Aislamiento entre pertenencias sucesivas (RF-062).
/// </summary>
/// <remarks>
/// La cascada alcanza los dependientes de la pertenencia que se cierra, no "todo lo de la persona".
/// Una pertenencia posterior abre asociaciones nuevas y propias, ajenas a las revocaciones previas:
/// si la revocación se determinara solo por <c>PersonaId</c>, un cambio de compañía arrastraría
/// también lo que la persona construya después.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AislamientoPertenenciasTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Una_pertenencia_posterior_abre_contextos_propios_no_afectados()
    {
        var (escenario, primera, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(primera.Id, DateTime.UtcNow.AddDays(-1)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Nueva pertenencia: la persona vuelve a ser contratada por la misma contratista.
        var segunda = await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        var nuevoContexto = await escenario.ContextoAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));

        var contextos = await escenario.ContextosEnBaseAsync();
        contextos.Should().HaveCount(2);

        var vigente = contextos.Single(c => c.Id == nuevoContexto.Id);

        // El contexto nuevo nace limpio: ninguna revocación previa lo alcanza.
        vigente.Estado.Should().Be(Estado.ACTIVO);
        vigente.MotivoFin.Should().BeNull();
        vigente.RevocadoPorPertenenciaId.Should().BeNull();

        segunda.Estado.Should().Be(EstadoPertenencia.ACTIVA);
    }

    [Fact]
    public async Task Cerrar_la_segunda_pertenencia_no_toca_lo_ya_revocado_por_la_primera()
    {
        var (escenario, primera, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(primera.Id, DateTime.UtcNow.AddDays(-1)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var revocadoPorLaPrimera = (await escenario.ContextosEnBaseAsync())[0];
        var finTrasPrimeraCascada = revocadoPorLaPrimera.FechaHoraFin;

        var segunda = await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        await escenario.ContextoAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));

        using (var cese = await escenario.FinalizarAsync(segunda.Id, DateTime.UtcNow.AddDays(30)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var antiguo = (await escenario.ContextosEnBaseAsync())
            .Single(c => c.Id == revocadoPorLaPrimera.Id);

        // Sigue atribuido a la primera pertenencia y con su fecha original: reescribirlo falsearía
        // la auditoría de un hecho ya ocurrido.
        antiguo.RevocadoPorPertenenciaId.Should().Be(primera.Id);
        antiguo.FechaHoraFin.Should().Be(finTrasPrimeraCascada);
    }

    [Fact]
    public async Task Las_credenciales_de_la_pertenencia_nueva_son_independientes()
    {
        var (escenario, primera, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(primera.Id, DateTime.UtcNow.AddDays(-1)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        await escenario.ContextoAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));

        var nueva = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));

        var credenciales = await escenario.CredencialesEnBaseAsync();
        credenciales.Should().HaveCount(2);

        var vigente = credenciales.Single(c => c.Id == nueva.Id);
        vigente.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        vigente.RevocadoPorPertenenciaId.Should().BeNull();

        // La credencial anterior sigue revocada por su propia pertenencia.
        credenciales.Single(c => c.Id != nueva.Id)
            .RevocadoPorPertenenciaId.Should().Be(primera.Id);
    }
}
