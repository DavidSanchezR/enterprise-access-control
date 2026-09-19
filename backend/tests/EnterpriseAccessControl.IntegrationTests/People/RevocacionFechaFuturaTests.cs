using System.Net;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Cese con fecha futura (CS-028, RF-064, research.md §14.3).
/// </summary>
/// <remarks>
/// La cascada se ejecuta de inmediato pero propaga la fecha futura, no la de hoy. Los dependientes
/// quedan marcados como cerrados y a la vez **siguen genuinamente utilizables** hasta que esa fecha
/// llegue, porque la vigencia efectiva se determina por fechas y no por el campo <c>Estado</c>. Esa
/// separación es la que evita tener que agendar ningún proceso para el futuro.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RevocacionFechaFuturaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Un_cese_futuro_propaga_esa_misma_fecha_de_inmediato()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        // Ejemplo de negocio: hoy 15/09, fin 30/09.
        var enQuinceDias = DateTime.UtcNow.AddDays(15);

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, enQuinceDias))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var esperada = Vigencia.NormalizarFin(enQuinceDias);

        (await escenario.PertenenciasEnBaseAsync())[0].FechaHoraFin.Should().Be(esperada);
        (await escenario.ContextosEnBaseAsync())[0].FechaHoraFin.Should().Be(esperada);
        (await escenario.UnidadesEnBaseAsync())[0].FechaHoraFin.Should().Be(esperada);
        (await escenario.CredencialesEnBaseAsync())[0].FechaHoraFin.Should().Be(esperada);
    }

    [Fact]
    public async Task Los_dependientes_siguen_vigentes_hasta_que_llegue_esa_fecha()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddDays(15)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var contexto = (await escenario.ContextosEnBaseAsync())[0];
        var credencial = (await escenario.CredencialesEnBaseAsync())[0];

        // Marcados como cerrados administrativamente...
        contexto.Estado.Should().Be(Estado.INACTIVO);
        credencial.Estado.Should().Be(EstadoCredencial.REVOCADA);

        // ...pero temporalmente vigentes: la autorización se decide por fechas (RF-063, §14.2).
        contexto.EstaVigenteEn(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task El_estado_efectivo_a_dia_de_hoy_sigue_mostrando_el_contexto()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddDays(15)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var hoy = await escenario.EstadoEfectivoAsync(DateTime.UtcNow);

        // El acceso no se corta antes de tiempo: es exactamente lo que pide el ejemplo de negocio.
        hoy.CompaniaVigenteId.Should().Be(escenario.Contratista.Id);
        hoy.ContextosOperativosVigentes.Should().ContainSingle();
    }

    [Fact]
    public async Task Tras_la_fecha_futura_el_estado_efectivo_queda_vacio()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddDays(15)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var despues = await escenario.EstadoEfectivoAsync(DateTime.UtcNow.AddDays(30));

        // Sin ningún proceso programado: la vigencia expira sola porque es una comparación de fechas.
        despues.CompaniaVigenteId.Should().BeNull();
        despues.ContextosOperativosVigentes.Should().BeEmpty();
    }

    [Fact]
    public async Task No_se_agenda_ningun_proceso_diferido()
    {
        // research.md §14.3 descartó explícitamente un planificador. La comprobación posible desde
        // aquí es que el efecto ya está escrito en el mismo instante del cese, sin esperar a nada.
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddMonths(6)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Inmediatamente después del cese, la disposición final ya está registrada.
        (await escenario.ContextosEnBaseAsync()).Should().OnlyContain(c =>
            c.Estado == Estado.INACTIVO
            && c.MotivoFin == MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA);
    }

    [Fact]
    public async Task Un_dependiente_que_termina_antes_de_la_fecha_futura_conserva_la_suya()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _c = escenario.Cliente;

        var pertenencia = await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contexto = await escenario.ContextoAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(3));

        var finOriginal = contexto.FechaHoraFin;

        using (var cese = await escenario.FinalizarAsync(
            pertenencia.Id, DateTime.UtcNow.AddDays(60)))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // La cascada acorta, nunca extiende: un contexto que ya terminaba antes no se alarga hasta
        // la fecha del cese.
        (await escenario.ContextosEnBaseAsync())[0].FechaHoraFin.Should().Be(finOriginal);
    }
}
