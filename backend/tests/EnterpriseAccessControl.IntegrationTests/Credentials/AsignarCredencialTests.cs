using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.Credentials;

/// <summary>
/// Alta de credencial por HTTP con los códigos de contracts/credentials.yaml (Historia 9, T154).
/// </summary>
/// <remarks>
/// Las reglas de fondo del alta —contexto vigente, contención, exclusividad por Principal— ya se
/// verifican en <c>People/CredencialesAsignacionTests</c> y <c>People/ContencionTemporalTests</c>.
/// Aquí se fija lo que aporta el controlador: ruta, cuerpo sin <c>personaId</c>, 201 y los códigos que
/// declara el contrato.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AsignarCredencialTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Asignar_devuelve_201_con_la_credencial_en_estado_ASIGNADO()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        using var respuesta = await escenario.PostCredencialAsync(escenario.PrincipalA.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creada = await respuesta.Content.ReadFromJsonAsync<AsignacionCredencialDto>(ApiFactory.Json);

        creada!.PersonaId.Should().Be(escenario.Persona.Id, "la persona viaja en la ruta");
        creada.CompaniaPrincipalId.Should().Be(escenario.PrincipalA.Id);
        creada.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        creada.RevocadoPorPertenenciaId.Should().BeNull();
    }

    [Fact]
    public async Task Una_compania_contratista_como_principal_se_rechaza_con_400()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        using var respuesta = await escenario.PostCredencialAsync(escenario.Contratista.Id);

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, CodigosError.CompaniaDebeSerPrincipal);
    }

    [Fact]
    public async Task Omitir_la_fecha_de_fin_se_rechaza_con_400()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            escenario.Ruta("credenciales"),
            new
            {
                companiaPrincipalId = escenario.PrincipalA.Id,
                tipoCredencialId = escenario.TipoCredencialId,
                fechaHoraInicio = DateTime.UtcNow,
            },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await escenario.ListarCredencialesAsync()).Should().BeEmpty();
    }

    public static TheoryData<string, int, int, int, int> Solapamientos => new()
    {
        // (caso, inicio previa, fin previa, inicio nueva, fin nueva) en días desde hoy.
        { "misma ventana", 0, 90, 0, 90 },
        { "la nueva empieza dentro de la previa", 0, 90, 30, 120 },
        { "la nueva termina dentro de la previa", 30, 120, 0, 60 },
        { "la nueva contiene a la previa", 30, 60, 0, 120 },
    };

    [Theory]
    [MemberData(nameof(Solapamientos))]
    public async Task Un_solapamiento_con_una_credencial_ASIGNADO_de_la_misma_principal_se_rechaza_con_409_sin_tocar_la_previa(
        string caso,
        int inicioPrevia,
        int finPrevia,
        int inicioNueva,
        int finNueva)
    {
        // Decisión A (Sesión 2026-09-15): se rechaza y la previa NO se cierra, finaliza, devuelve,
        // elimina ni revoca (RF-057).
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var hoy = DateTime.UtcNow;

        var previa = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, hoy.AddDays(inicioPrevia), hoy.AddDays(finPrevia));

        var antes = await escenario.LeerCredencialAsync(previa.Id);

        using var respuesta = await escenario.PostCredencialAsync(
            escenario.PrincipalA.Id, hoy.AddDays(inicioNueva), hoy.AddDays(finNueva));

        await EsperarProblemaAsync(respuesta, HttpStatusCode.Conflict, CodigosError.SolapamientoVigencia);

        var despues = await escenario.LeerCredencialAsync(previa.Id);

        despues.Estado.Should().Be(EstadoCredencial.ASIGNADO, caso);
        despues.FechaHoraInicio.Should().Be(antes.FechaHoraInicio, caso);
        despues.FechaHoraFin.Should().Be(antes.FechaHoraFin, caso);
        despues.RevocadoPorPertenenciaId.Should().BeNull(caso);
        despues.UpdatedAt.Should().Be(antes.UpdatedAt, "{0}: la fila no debe haberse reescrito", caso);
        despues.RowVersion.Should().Equal(antes.RowVersion, "{0}: ninguna escritura sobre la previa", caso);

        (await escenario.ListarCredencialesAsync(escenario.PrincipalA.Id))
            .Should().ContainSingle(caso).Which.Id.Should().Be(previa.Id);
    }

    [Fact]
    public async Task Una_credencial_sin_solapamiento_se_admite_y_tampoco_modifica_la_previa()
    {
        // Contrapunto: aun cuando la nueva es legítima, asignarla no cierra la previa (decisión A).
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var hoy = DateTime.UtcNow;

        var previa = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, hoy, hoy.AddDays(30));

        var antes = await escenario.LeerCredencialAsync(previa.Id);

        using var respuesta = await escenario.PostCredencialAsync(
            escenario.PrincipalA.Id, hoy.AddDays(40), hoy.AddDays(90));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var despues = await escenario.LeerCredencialAsync(previa.Id);

        despues.Estado.Should().Be(EstadoCredencial.ASIGNADO);
        despues.FechaHoraFin.Should().Be(antes.FechaHoraFin);
        despues.RowVersion.Should().Equal(antes.RowVersion);
    }

    [Fact]
    public async Task El_rechazo_por_solapamiento_lo_decide_la_aplicacion_antes_de_escribir()
    {
        // research.md §5: la aplicación valida primero y devuelve un ProblemDetails legible; el trigger
        // es la última defensa ante escrituras concurrentes. El detalle que emite el servicio lo prueba.
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        using var respuesta = await escenario.PostCredencialAsync(escenario.PrincipalA.Id);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);

        problema!.Detail.Should().Contain("RF-057");
    }

    [Fact]
    public async Task Una_principal_fuera_del_alcance_del_usuario_devuelve_404()
    {
        var escenario = await CredencialesSoporte.MontarConContextosAsync(fixture);
        var ajena = await fixture.Api.SembrarCompaniaAsync($"Principal ajena {Guid.CreateVersion7():N}"[..30]);

        using var respuesta = await escenario.PostCredencialAsync(ajena.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task EsperarProblemaAsync(
        HttpResponseMessage respuesta,
        HttpStatusCode estado,
        string codigo)
    {
        respuesta.StatusCode.Should().Be(estado);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(codigo);
    }
}
