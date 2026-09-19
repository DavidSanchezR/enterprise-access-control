using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Caso A (RF-053): la persona pertenece a una Compañía PRINCIPAL_MANDANTE.
/// </summary>
/// <remarks>
/// Su contexto operativo solo puede ser con esa misma Principal. Permitirle abrir contexto con otra
/// significaría que un trabajador de una minera puede operar en otra sin ningún vínculo contractual
/// que lo respalde.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContextoOperativoCasoATests(SqlServerFixture fixture)
{
    private Task<EscenarioUs5> MontarAsync() => new EscenarioUs5(fixture).MontarAsync();

    [Fact]
    public async Task Una_persona_de_una_principal_abre_contexto_con_esa_misma_principal()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // La pertenencia es directamente con la Principal A.
        await escenario.PertenenciaVigenteAsync(escenario.PrincipalA.Id);

        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);

        contexto.CompaniaPrincipalId.Should().Be(escenario.PrincipalA.Id);
        contexto.PersonaId.Should().Be(escenario.Persona.Id);
    }

    [Fact]
    public async Task Una_persona_de_una_principal_no_puede_abrir_contexto_con_otra()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.PrincipalA.Id);

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        // CS-020: sin pertenencia ni relación contratista que lo justifique, no hay vínculo alguno
        // con la Principal B.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString()
            .Should().Be("PRINCIPAL_NO_CORRESPONDE_A_PERTENENCIA");
    }

    [Fact]
    public async Task Una_relacion_contratista_ajena_no_habilita_a_la_persona_de_una_principal()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // Existe relación Contratista→PrincipalB en el escenario, pero esta persona pertenece a la
        // Principal A, no a la contratista: esa relación no le concede nada.
        await escenario.PertenenciaVigenteAsync(escenario.PrincipalA.Id);

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sin_pertenencia_vigente_no_se_puede_abrir_ningun_contexto()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        // La persona existe pero nadie la ha contratado: no hay ancla que sustente el contexto
        // (research.md §13).
        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalA.Id);

        // contracts/people.yaml enumera este caso entre los de 400: es una precondición de la
        // propia petición que no se cumple, no un conflicto con el estado de otro recurso.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("SIN_PERTENENCIA_VIGENTE");
    }

    [Fact]
    public async Task No_se_puede_abrir_contexto_con_una_compania_contratista()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.PrincipalA.Id);

        // RF-053/RF-054: el contexto operativo es siempre con una PRINCIPAL_MANDANTE.
        using var respuesta = await escenario.AbrirContextoAsync(escenario.Contratista.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");
    }

    [Fact]
    public async Task Una_compania_principal_fuera_de_alcance_devuelve_404()
    {
        var escenario = await MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.PrincipalA.Id);

        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {EscenarioUs5.Sufijo()}");

        using var respuesta = await escenario.AbrirContextoAsync(ajena.Id);

        // 404 y no 400: revelar que existe permitiría enumerar compañías ajenas.
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
