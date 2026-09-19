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
/// Asignación de unidad organizativa dentro de un contexto operativo (RF-015, RF-055, CS-014).
/// </summary>
/// <remarks>
/// La exclusividad es por contexto, no por persona: el trigger se particiona por
/// <c>ContextoOperativoId</c>. Esa elección es la que permite que la misma persona tenga una unidad
/// vigente en cada una de las Principales para las que trabaja.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AsignacionUnidadOrganizativaTests(SqlServerFixture fixture)
{
    private async Task<(EscenarioUs5 Escenario, ContextoOperativoDto Contexto, Guid Unidad)>
        MontarConContextoAsync()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var unidad = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);

        return (escenario, contexto, unidad);
    }

    [Fact]
    public async Task Asignar_una_unidad_dentro_de_un_contexto_vigente()
    {
        var (escenario, contexto, unidad) = await MontarConContextoAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.AsignarUnidadAsync(contexto.Id, unidad);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creada = await respuesta.Content
            .ReadFromJsonAsync<AsignacionUnidadOrganizativaDto>(ApiFactory.Json);

        creada!.ContextoOperativoId.Should().Be(contexto.Id);
        creada.UnidadOrganizativaId.Should().Be(unidad);
        creada.Estado.Should().Be(Estado.ACTIVO);

        // PersonaId está denormalizado para evitar un join en la consulta más frecuente.
        creada.PersonaId.Should().Be(escenario.Persona.Id);
    }

    [Fact]
    public async Task Asignar_una_unidad_nueva_cierra_la_anterior_del_mismo_contexto()
    {
        var (escenario, contexto, primera) = await MontarConContextoAsync();
        using var _ = escenario.Cliente;

        using (var inicial = await escenario.AsignarUnidadAsync(contexto.Id, primera))
        {
            inicial.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var segunda = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id, "Taller");

        using (var reemplazo = await escenario.AsignarUnidadAsync(
            contexto.Id, segunda, DateTime.UtcNow.AddDays(1)))
        {
            reemplazo.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var unidades = await escenario.UnidadesEnBaseAsync();
        unidades.Should().HaveCount(2);

        var cerrada = unidades.Single(u => u.UnidadOrganizativaId == primera);
        cerrada.Estado.Should().Be(Estado.INACTIVO);
        cerrada.MotivoFin.Should().Be(MotivoFinRevocacion.REEMPLAZO_ASIGNACION);

        // Un reemplazo no es una revocación en cascada.
        cerrada.RevocadoPorPertenenciaId.Should().BeNull();
    }

    [Fact]
    public async Task Dos_asignaciones_solapadas_en_el_mismo_contexto_se_rechazan()
    {
        var (escenario, contexto, unidad) = await MontarConContextoAsync();
        using var _ = escenario.Cliente;

        using (var primera = await escenario.AsignarUnidadAsync(contexto.Id, unidad))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var otra = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id, "Bodega");

        // Mismo inicio que la vigente: no hay forma de cerrarla antes de que empiece la nueva.
        using var solapada = await escenario.AsignarUnidadAsync(contexto.Id, otra);

        solapada.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task El_trigger_respalda_la_exclusividad_por_contexto()
    {
        var (escenario, contexto, unidad) = await MontarConContextoAsync();
        using var _ = escenario.Cliente;

        using (var primera = await escenario.AsignarUnidadAsync(contexto.Id, unidad))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await escenario.ConDatosAsync(async db =>
        {
            db.Set<AsignacionPersonaUnidadOrganizativa>().Add(
                new AsignacionPersonaUnidadOrganizativa
                {
                    PersonaId = escenario.Persona.Id,
                    ContextoOperativoId = contexto.Id,
                    UnidadOrganizativaId = unidad,
                    FechaHoraInicio = DateTime.UtcNow,
                    FechaHoraFin = DateTime.UtcNow.AddMonths(6),
                });

            // Condición de carrera simulada: la base de datos es la última línea de defensa.
            var guardar = async () => await db.SaveChangesAsync();
            var error = await guardar.Should()
                .ThrowAsync<DbUpdateException>()
                .WithInnerException<DbUpdateException, Microsoft.Data.SqlClient.SqlException>();

            error.Which.Message.Should().Contain("Solapamiento");
        });
    }

    [Fact]
    public async Task Contextos_distintos_de_la_misma_persona_admiten_unidades_simultaneas()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contextoA = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var contextoB = await escenario.ContextoAsync(escenario.PrincipalB.Id);

        var unidadA = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);
        var unidadB = await escenario.SembrarUnidadAsync(escenario.PrincipalB.Id);

        using (var a = await escenario.AsignarUnidadAsync(contextoA.Id, unidadA))
        {
            a.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // CS-014: rangos idénticos, contextos distintos. Si el trigger se particionara por
        // PersonaId, esto se rechazaría y el modelo multi-Principal sería inviable.
        using var b = await escenario.AsignarUnidadAsync(contextoB.Id, unidadB);

        b.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Una_unidad_de_otra_principal_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contextoA = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var unidadDeB = await escenario.SembrarUnidadAsync(escenario.PrincipalB.Id);

        using var respuesta = await escenario.AsignarUnidadAsync(contextoA.Id, unidadDeB);

        // RF-055: asignar una unidad de otra Principal daría a la persona una ubicación
        // organizativa que su contexto no justifica.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content
            .ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(ApiFactory.Json);

        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");
    }

    [Fact]
    public async Task Un_contexto_inexistente_devuelve_404()
    {
        var (escenario, _, unidad) = await MontarConContextoAsync();
        using var _c = escenario.Cliente;

        using var respuesta = await escenario.AsignarUnidadAsync(Guid.CreateVersion7(), unidad);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Un_contexto_de_otra_persona_devuelve_404()
    {
        var (escenario, contexto, unidad) = await MontarConContextoAsync();
        using var _ = escenario.Cliente;

        var otra = await new EscenarioUs5(fixture).MontarAsync();
        using var _o = otra.Cliente;

        // El contexto existe, pero no es de esta persona: la ruta lo exige como suyo.
        using var respuesta = await otra.AsignarUnidadAsync(contexto.Id, unidad);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task El_listado_devuelve_el_historico_del_contexto()
    {
        var (escenario, contexto, primera) = await MontarConContextoAsync();
        using var _ = escenario.Cliente;

        using (var a = await escenario.AsignarUnidadAsync(contexto.Id, primera))
        {
            a.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var segunda = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id, "Taller");

        using (var b = await escenario.AsignarUnidadAsync(
            contexto.Id, segunda, DateTime.UtcNow.AddDays(1)))
        {
            b.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var historico = await escenario.Cliente
            .GetFromJsonAsync<IReadOnlyList<AsignacionUnidadOrganizativaDto>>(
                escenario.Ruta($"contextos-operativos/{contexto.Id}/unidad-organizativa"),
                ApiFactory.Json);

        // La asignación cerrada permanece: es histórico, no se elimina (CS-026).
        historico.Should().HaveCount(2);
    }
}
