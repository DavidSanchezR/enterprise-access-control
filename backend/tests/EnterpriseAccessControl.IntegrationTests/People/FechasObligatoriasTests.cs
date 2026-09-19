using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Fechas obligatorias en toda asociación temporal de persona (RF-071).
/// </summary>
/// <remarks>
/// No existe vigencia indefinida: ni <c>null</c> ni fecha centinela como 9999-12-31. La regla se
/// comprueba en los dos niveles que importan —el borde HTTP, que debe rechazar con 400, y la columna
/// de la base de datos, que debe ser NOT NULL— porque uno sin el otro deja un hueco por el que la
/// vigencia abierta podría reaparecer.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class FechasObligatoriasTests(SqlServerFixture fixture)
{
    private async Task<EscenarioUs5> MontarConPertenenciaAsync()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        return escenario;
    }

    [Fact]
    public async Task Una_pertenencia_sin_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta("historial-companias"),
            new { companiaId = escenario.Contratista.Id, fechaHoraInicio = DateTime.UtcNow },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Un_contexto_operativo_sin_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta("contextos-operativos"),
            new { companiaPrincipalId = escenario.PrincipalA.Id, fechaHoraInicio = DateTime.UtcNow },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_asignacion_de_unidad_sin_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var unidad = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta($"contextos-operativos/{contexto.Id}/unidad-organizativa"),
            new { unidadOrganizativaId = unidad, fechaHoraInicio = DateTime.UtcNow },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_perfil_sin_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        var tipoPersonaId = await ContencionTemporalTests.SembrarTipoPersonaAsync(escenario);

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta("perfiles"),
            new { tipoPersonaId, fechaHoraInicio = DateTime.UtcNow },
            ApiFactory.Json);

        // RF-071 también le aplica al perfil, aunque RF-072 (contención) no.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_peticion_sin_fecha_de_inicio_tambien_se_rechaza_con_400()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta("historial-companias"),
            new { companiaId = escenario.Contratista.Id, fechaHoraFin = DateTime.UtcNow.AddYears(1) },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("AsignacionPersonaCompania")]
    [InlineData("ContextoOperativoPersonaPrincipal")]
    [InlineData("AsignacionPersonaUnidadOrganizativa")]
    [InlineData("AsignacionCredencial")]
    [InlineData("AsignacionTipoPersona")]
    public async Task La_columna_de_fecha_de_fin_es_NOT_NULL_en_la_base_de_datos(string tabla)
    {
        // El borde puede fallar o cambiar; la columna es la garantía última de que no reaparezca la
        // vigencia indefinida.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var esAnulable = await db.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT IS_NULLABLE AS [Value]
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = {0} AND COLUMN_NAME = 'FechaHoraFin'
                    """,
                    tabla)
                .SingleAsync();

            esAnulable.Should().Be("NO", "RF-071 no admite vigencia indefinida en {0}", tabla);
        });
    }

    [Fact]
    public async Task La_relacion_contratista_principal_SI_admite_fecha_de_fin_nula()
    {
        // Contrapunto deliberado: RF-071 aplica a las asociaciones vinculadas a una *persona*. Ésta
        // vincula dos compañías, y null conserva su significado de vigencia abierta. Si alguien la
        // volviera obligatoria por arrastre, esta prueba lo detecta.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var esAnulable = await db.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT IS_NULLABLE AS [Value]
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'RelacionContratistaPrincipal' AND COLUMN_NAME = 'FechaHoraFin'
                    """)
                .SingleAsync();

            esAnulable.Should().Be("YES");
        });
    }

    [Fact]
    public async Task Ninguna_asociacion_usa_una_fecha_centinela()
    {
        var escenario = await MontarConPertenenciaAsync();
        using var _ = escenario.Cliente;

        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        // RF-071 prohíbe tanto null como un valor centinela del tipo 9999-12-31: la fecha debe ser
        // real y conocida desde la creación.
        var centinela = new DateTime(9000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        (await escenario.PertenenciasEnBaseAsync())
            .Should().OnlyContain(p => p.FechaHoraFin < centinela);

        (await escenario.ContextosEnBaseAsync())
            .Should().OnlyContain(c => c.FechaHoraFin < centinela);
    }
}
