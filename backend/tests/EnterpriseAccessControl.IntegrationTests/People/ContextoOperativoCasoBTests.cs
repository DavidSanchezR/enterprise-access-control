using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Caso B (RF-054): la persona pertenece a una Compañía CONTRATISTA.
/// </summary>
/// <remarks>
/// Solo puede operar con las Principales con las que su contratista tenga relación vigente. Es la
/// regla que impide que un trabajador de una contratista acceda a una minera con la que su empleador
/// no tiene contrato.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class ContextoOperativoCasoBTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Una_persona_de_una_contratista_opera_con_la_principal_relacionada()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);

        contexto.CompaniaPrincipalId.Should().Be(escenario.PrincipalA.Id);
    }

    [Fact]
    public async Task Sin_relacion_contratista_principal_vigente_se_rechaza()
    {
        // El escenario se monta sin relación hacia la Principal B.
        var escenario = await new EscenarioUs5(fixture).MontarAsync(conRelacionB: false);
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        // CS-019: sin contrato entre las compañías, el trabajador no tiene nada que hacer allí.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString()
            .Should().Be("SIN_RELACION_CONTRATISTA_PRINCIPAL_VIGENTE");
    }

    [Fact]
    public async Task Una_relacion_ya_finalizada_no_habilita_el_contexto()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync(conRelacionB: false);
        using var _ = escenario.Cliente;

        // Relación que existió pero terminó: la vigencia se evalúa por fechas, no por su existencia.
        await escenario.ConDatosAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = escenario.Contratista.Id,
                CompaniaPrincipalId = escenario.PrincipalB.Id,
                FechaHoraInicio = DateTime.UtcNow.AddYears(-2),
                FechaHoraFin = DateTime.UtcNow.AddMonths(-3),
            });

            await db.SaveChangesAsync();
        });

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_relacion_todavia_futura_no_habilita_el_contexto()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync(conRelacionB: false);
        using var _ = escenario.Cliente;

        await escenario.ConDatosAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = escenario.Contratista.Id,
                CompaniaPrincipalId = escenario.PrincipalB.Id,
                FechaHoraInicio = DateTime.UtcNow.AddMonths(3),
                FechaHoraFin = null,
            });

            await db.SaveChangesAsync();
        });

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_relacion_de_otra_contratista_no_habilita_a_esta_persona()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync(conRelacionB: false);
        using var _ = escenario.Cliente;

        // Otra contratista sí tiene relación con la Principal B, pero esta persona no pertenece a
        // ella: la habilitación es de la compañía de pertenencia, no del sector.
        var otraContratista = await fixture.Api.SembrarCompaniaAsync(
            $"Otra {EscenarioUs5.Sufijo()}", Domain.Enums.TipoCompania.CONTRATISTA);

        await escenario.SembrarRelacionAsync(otraContratista.Id, escenario.PrincipalB.Id);

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        using var respuesta = await escenario.AbrirContextoAsync(escenario.PrincipalB.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Una_relacion_de_vigencia_abierta_habilita_el_contexto()
    {
        // La relación Contratista↔Principal sí admite fechaHoraFin nula (RF-071 no le aplica, porque
        // no está vinculada a una persona). Debe contar como vigente.
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        using var _ = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var contexto = await escenario.ContextoAsync(escenario.PrincipalB.Id);

        contexto.CompaniaPrincipalId.Should().Be(escenario.PrincipalB.Id);
    }
}
