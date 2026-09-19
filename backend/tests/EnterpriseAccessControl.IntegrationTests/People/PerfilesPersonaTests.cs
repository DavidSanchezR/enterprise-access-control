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
/// Perfiles de persona simultáneos, sin exclusividad mutua (RF-011).
/// </summary>
/// <remarks>
/// A diferencia de la compañía de pertenencia y de la unidad organizativa, una persona puede tener
/// varios perfiles vigentes a la vez —"uno o varios", dice RF-011—. Por eso esta entidad no tiene
/// trigger de no-solapamiento: no hay exclusividad que imponer.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PerfilesPersonaTests(SqlServerFixture fixture)
{
    private async Task<EscenarioUs5> MontarAsync()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        return escenario;
    }

    [Fact]
    public async Task Una_persona_puede_tener_varios_perfiles_vigentes_a_la_vez()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var trabajador = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);
        var visitante = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        foreach (var tipo in new[] { trabajador, visitante })
        {
            using var respuesta = await escenario.AsignarPerfilAsync(tipo);
            respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var perfiles = await escenario.Cliente
            .GetFromJsonAsync<IReadOnlyList<AsignacionTipoPersonaDto>>(
                escenario.Ruta("perfiles"), ApiFactory.Json);

        // Rangos idénticos, perfiles distintos: RF-011 no impone exclusividad.
        perfiles.Should().HaveCount(2);
        perfiles.Should().OnlyContain(p => p.Estado == Estado.ACTIVO);
    }

    [Fact]
    public async Task El_mismo_perfil_puede_repetirse_en_periodos_distintos()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        using (var primero = await escenario.AsignarPerfilAsync(
            tipo, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-3)))
        {
            primero.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var segundo = await escenario.AsignarPerfilAsync(
            tipo, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddMonths(6));

        segundo.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task No_existe_trigger_de_no_solapamiento_para_los_perfiles()
    {
        // Es la contrapartida estructural de RF-011: si alguien añadiera un trigger particionado por
        // PersonaId, los perfiles simultáneos dejarían de ser posibles.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var triggers = await db.Database
                .SqlQueryRaw<int>(
                    """
                    SELECT COUNT(*) AS [Value]
                    FROM sys.triggers t
                    JOIN sys.tables tb ON t.parent_id = tb.object_id
                    WHERE tb.name = 'AsignacionTipoPersona'
                    """)
                .SingleAsync();

            triggers.Should().Be(0);
        });
    }

    [Fact]
    public async Task Un_tipo_de_persona_inactivo_no_puede_asignarse()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        await escenario.ConDatosAsync(async db =>
        {
            var entidad = await db.Set<TipoPersona>().FirstAsync(t => t.Id == tipo);
            entidad.Estado = Estado.INACTIVO;
            await db.SaveChangesAsync();
        });

        using var respuesta = await escenario.AsignarPerfilAsync(tipo);

        // RF-032: un valor de catálogo inactivo deja de estar disponible para asignaciones nuevas,
        // sin afectar al histórico que ya lo referencia.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_tipo_de_persona_inexistente_se_rechaza()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.AsignarPerfilAsync(Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Los_perfiles_se_normalizan_a_dias_completos()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        using var respuesta = await escenario.AsignarPerfilAsync(
            tipo,
            new DateTime(2026, 9, 1, 15, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 8, 15, 0, DateTimeKind.Utc));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creado = await respuesta.Content
            .ReadFromJsonAsync<AsignacionTipoPersonaDto>(ApiFactory.Json);

        creado!.FechaHoraInicio.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        creado.FechaHoraFin.Should().Be(new DateTime(2026, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Los_perfiles_no_se_revocan_al_cerrar_la_pertenencia()
    {
        // RF-072 no declara al perfil dependiente de la pertenencia, así que la cascada de RF-061 no
        // debe alcanzarlo. Revocarlo sería inventar una dependencia que el dominio no establece.
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        using (var asignado = await escenario.AsignarPerfilAsync(tipo))
        {
            asignado.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var pertenencia = (await escenario.PertenenciasEnBaseAsync())[0];

        using (var cese = await escenario.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        await escenario.ConDatosAsync(async db =>
        {
            var perfiles = await db.Set<AsignacionTipoPersona>()
                .AsNoTracking()
                .Where(p => p.PersonaId == escenario.Persona.Id)
                .ToListAsync();

            perfiles.Should().OnlyContain(p => p.Estado == Estado.ACTIVO);
        });
    }
}
