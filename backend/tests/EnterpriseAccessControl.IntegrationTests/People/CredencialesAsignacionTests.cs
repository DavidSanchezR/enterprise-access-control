using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Asignación de credenciales por Compañía Principal (RF-018, RF-056, RF-057, CS-016, CS-017).
/// </summary>
/// <remarks>
/// La Historia 5 implementó el alta, que es lo que la cascada necesita. Desde la Historia 9 el alta
/// se ejercita por HTTP contra el controlador; devolver y dar de baja se prueban en
/// <c>Credentials/</c>.
///
/// La exclusividad es por par (persona, Principal) y solo entre credenciales <c>ASIGNADO</c>: una
/// persona puede portar credenciales de dos mineras a la vez, pero no dos de la misma.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class CredencialesAsignacionTests(SqlServerFixture fixture)
{
    private async Task<EscenarioUs5> MontarConContextosAsync()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);
        await escenario.ContextoAsync(escenario.PrincipalB.Id);

        return escenario;
    }

    [Fact]
    public async Task Asignar_una_credencial_dentro_de_un_contexto_vigente()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        var credencial = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        credencial.PersonaId.Should().Be(escenario.Persona.Id);
        credencial.CompaniaPrincipalId.Should().Be(escenario.PrincipalA.Id);
        credencial.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        credencial.RevocadoPorPertenenciaId.Should().BeNull();
    }

    [Fact]
    public async Task Sin_contexto_operativo_vigente_con_esa_principal_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        // Hay pertenencia, pero ningún contexto con esa Principal: RF-056 exige el contexto como
        // respaldo. Una credencial sin él no representaría ninguna autorización real.
        using var respuesta = await escenario.PostCredencialAsync(escenario.PrincipalA.Id);

        // contracts/credentials.yaml: 400 cuando no existe contexto operativo vigente.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(CodigosError.SinContextoOperativoVigente);
    }

    [Fact]
    public async Task Credenciales_simultaneas_para_principales_distintas_se_admiten()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        var deA = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);
        var deB = await escenario.AsignarCredencialAsync(escenario.PrincipalB.Id);

        // CS-016/CS-017: mismo rango temporal, Principales distintas. Si el trigger se particionara
        // solo por PersonaId, el modelo multi-Principal sería inviable.
        deA.FechaHoraInicio.Should().Be(deB.FechaHoraInicio);

        var credenciales = await escenario.CredencialesEnBaseAsync();
        credenciales.Should().HaveCount(2);
        credenciales.Should().OnlyContain(c => c.Estado == EstadoCredencial.ASIGNADO);
    }

    [Fact]
    public async Task El_trigger_rechaza_dos_credenciales_ASIGNADO_solapadas_de_la_misma_principal()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        await escenario.ConDatosAsync(async db =>
        {
            db.Set<AsignacionCredencial>().Add(new AsignacionCredencial
            {
                PersonaId = escenario.Persona.Id,
                CompaniaPrincipalId = escenario.PrincipalA.Id,
                TipoCredencialId = escenario.TipoCredencialId,
                FechaHoraInicio = DateTime.UtcNow,
                FechaHoraFin = DateTime.UtcNow.AddMonths(3),
                Estado = EstadoCredencial.ASIGNADO,
            });

            // RF-057: una sola credencial activa por par (persona, Principal).
            var guardar = async () => await db.SaveChangesAsync();
            var error = await guardar.Should()
                .ThrowAsync<DbUpdateException>()
                .WithInnerException<DbUpdateException, Microsoft.Data.SqlClient.SqlException>();

            error.Which.Message.Should().Contain("Solapamiento");
        });
    }

    [Fact]
    public async Task Una_credencial_DEVUELTA_no_reserva_la_ventana()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        var original = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        // Devolución manual (baja lógica): la operación completa es de la Historia 9, pero el efecto
        // sobre la exclusividad se puede comprobar ya.
        await escenario.ConDatosAsync(async db =>
        {
            var entidad = await db.Set<AsignacionCredencial>().FirstAsync(c => c.Id == original.Id);
            entidad.Estado = EstadoCredencial.DEVUELTO;
            await db.SaveChangesAsync();
        });

        // El trigger solo compara entre filas ASIGNADO, así que puede emitirse una de reemplazo.
        var reemplazo = await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        reemplazo.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        (await escenario.CredencialesEnBaseAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task Un_tipo_de_credencial_inactivo_se_rechaza()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        await escenario.ConDatosAsync(async db =>
        {
            var tipo = await db.Set<TipoCredencial>()
                .FirstAsync(t => t.Id == escenario.TipoCredencialId);

            tipo.Estado = Estado.INACTIVO;
            await db.SaveChangesAsync();
        });

        using var respuesta = await escenario.PostCredencialAsync(escenario.PrincipalA.Id);

        // RF-032: un valor de catálogo inactivo deja de usarse en asignaciones nuevas.
        // contracts/credentials.yaml lo declara 409 para este endpoint.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(CodigosError.ValorMaestroInactivo);
    }

    [Fact]
    public async Task La_credencial_vigente_requiere_estado_ASIGNADO_y_estar_en_ventana()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        var credencial = (await escenario.CredencialesEnBaseAsync())[0];

        credencial.EstaVigenteEn(DateTime.UtcNow).Should().BeTrue();

        // RF-070: fuera de la ventana no está vigente, aunque su estado siga ASIGNADO — el mero
        // vencimiento no dispara ninguna transición de estado.
        credencial.EstaVigenteEn(credencial.FechaHoraFin.AddDays(1)).Should().BeFalse();
        credencial.Estado.Should().Be(EstadoCredencial.ASIGNADO);
    }

    [Fact]
    public async Task Las_fechas_de_la_credencial_se_normalizan_a_dias_completos()
    {
        var escenario = await MontarConContextosAsync();
        using var _ = escenario.Cliente;

        var credencial = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddMonths(3));

        credencial.FechaHoraInicio.TimeOfDay.Should().Be(TimeSpan.Zero);
        credencial.FechaHoraFin.TimeOfDay.Should().Be(
            new TimeSpan(0, 23, 59, 59, 999));
    }
}
