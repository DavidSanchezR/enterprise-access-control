using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Alta de pertenencia: normalización de fechas, obligatoriedad del fin, cierre automático de la
/// anterior y no-solapamiento respaldado por trigger (RF-014, RF-016, RF-039, RF-071).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class HistorialPersonaTests(SqlServerFixture fixture)
{
    private Task<EscenarioUs5> MontarAsync() => new EscenarioUs5(fixture).MontarAsync();

    [Fact]
    public async Task Las_fechas_se_normalizan_a_inicio_y_fin_de_dia()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var inicio = new DateTime(2026, 8, 1, 14, 37, 22, DateTimeKind.Utc);
        var fin = new DateTime(2027, 7, 31, 9, 5, 0, DateTimeKind.Utc);

        var creada = await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, inicio, fin);

        // RF-016: las vigencias se declaran en días completos, no en instantes arbitrarios.
        creada.FechaHoraInicio.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        creada.FechaHoraFin.Should().Be(new DateTime(2027, 7, 31, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Fact]
    public async Task La_pertenencia_nace_ACTIVA_y_sin_motivo_de_fin()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var creada = await escenario.PertenenciaVigenteAsync();

        creada.Estado.Should().Be(EstadoPertenencia.ACTIVA);
        creada.MotivoFin.Should().BeNull();
    }

    [Fact]
    public async Task Omitir_la_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // RF-071: no existe pertenencia de vigencia indefinida. Se envía el cuerpo sin el campo
        // para comprobar que el contrato lo exige de verdad, no solo en la documentación.
        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta("historial-companias"),
            new { companiaId = escenario.Contratista.Id, fechaHoraInicio = DateTime.UtcNow },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_rango_invertido_se_rechaza()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.CrearPertenenciaAsync(
            escenario.Contratista.Id,
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Crear_una_nueva_pertenencia_cierra_la_anterior()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var primera = await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddYears(1));

        var inicioSegunda = DateTime.UtcNow.AddDays(1);

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, inicioSegunda, DateTime.UtcNow.AddYears(2));

        var pertenencias = await escenario.PertenenciasEnBaseAsync();
        pertenencias.Should().HaveCount(2);

        var cerrada = pertenencias.Single(p => p.Id == primera.Id);

        // RF-014: solo una pertenencia activa a la vez. El cierre registra el motivo.
        cerrada.Estado.Should().Be(EstadoPertenencia.FINALIZADA);
        cerrada.MotivoFin.Should().Be(MotivoFinPertenencia.REEMPLAZO_ASIGNACION);
        cerrada.FechaHoraFin.Should().BeCloseTo(
            Vigencia.NormalizarInicio(inicioSegunda), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task El_cierre_no_extiende_una_pertenencia_que_ya_habia_vencido()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // Vencida de forma natural hace un mes.
        var vencida = await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-1));

        var finOriginal = vencida.FechaHoraFin;

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        var pertenencias = await escenario.PertenenciasEnBaseAsync();
        var cerrada = pertenencias.Single(p => p.Id == vencida.Id);

        // Un cierre solo acorta. Alargarla hasta hoy concedería una vigencia que nunca existió.
        cerrada.FechaHoraFin.Should().Be(finOriginal);
    }

    [Fact]
    public async Task El_trigger_rechaza_un_solapamiento_insertado_saltandose_la_aplicacion()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync();

        await escenario.ConDatosAsync(async db =>
        {
            db.Set<AsignacionPersonaCompania>().Add(new AsignacionPersonaCompania
            {
                PersonaId = escenario.Persona.Id,
                CompaniaId = escenario.PrincipalA.Id,
                FechaHoraInicio = DateTime.UtcNow,
                FechaHoraFin = DateTime.UtcNow.AddMonths(6),
            });

            // Simula la condición de carrera que la validación de aplicación no puede evitar por sí
            // sola: dos altas concurrentes que superan la comprobación previa antes de escribir.
            var guardar = async () => await db.SaveChangesAsync();
            var error = await guardar.Should()
                .ThrowAsync<DbUpdateException>()
                .WithInnerException<DbUpdateException, Microsoft.Data.SqlClient.SqlException>();

            error.Which.Message.Should().Contain("Solapamiento");
        });
    }

    [Fact]
    public async Task El_trigger_admite_pertenencias_consecutivas_en_dias_contiguos()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // Frontera: el fin de una es 23:59:59.999 y el inicio de la siguiente, 00:00 del día
        // siguiente. Es la propiedad que hace utilizable la normalización.
        await escenario.ConDatosAsync(async db =>
        {
            db.Set<AsignacionPersonaCompania>().AddRange(
                new AsignacionPersonaCompania
                {
                    PersonaId = escenario.Persona.Id,
                    CompaniaId = escenario.Contratista.Id,
                    FechaHoraInicio = Vigencia.NormalizarInicio(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    FechaHoraFin = Vigencia.NormalizarFin(new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
                },
                new AsignacionPersonaCompania
                {
                    PersonaId = escenario.Persona.Id,
                    CompaniaId = escenario.PrincipalA.Id,
                    FechaHoraInicio = Vigencia.NormalizarInicio(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
                    FechaHoraFin = Vigencia.NormalizarFin(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)),
                });

            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().NotThrowAsync();
        });
    }

    [Fact]
    public async Task El_historial_lista_todas_las_pertenencias_de_la_persona()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(
            escenario.Contratista.Id, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-3));

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddYears(1));

        var historial = await escenario.Cliente.GetFromJsonAsync<IReadOnlyList<AsignacionCompaniaDto>>(
            escenario.Ruta("historial-companias"), ApiFactory.Json);

        // El histórico conserva ambas: una pertenencia cerrada nunca se elimina (CS-026).
        historial.Should().HaveCount(2);
    }

    [Fact]
    public async Task No_se_puede_crear_una_pertenencia_con_una_compania_fuera_de_alcance()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {EscenarioUs5.Sufijo()}");

        using var respuesta = await escenario.CrearPertenenciaAsync(ajena.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
