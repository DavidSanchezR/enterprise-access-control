using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.OrgUnits;

/// <summary>
/// Una compañía CONTRATISTA no puede poseer unidades organizativas (RF-045, Historia 2 criterio 5).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class UnidadRaizContratistaRechazadaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Crear_una_unidad_raiz_sobre_una_contratista_se_rechaza_con_400()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Servicios",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(contratista.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/unidades-organizativas", UriKind.Relative),
            new UnidadOrganizativaRequest("Gerencia", Estado.ACTIVO, null, contratista.Id),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");
    }

    [Fact]
    public async Task El_rechazo_no_deja_ninguna_unidad_ni_enlace_creados()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Limpia",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(contratista.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/unidades-organizativas", UriKind.Relative),
            new UnidadOrganizativaRequest("Fantasma", Estado.ACTIVO, null, contratista.Id),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // La validación ocurre antes de escribir nada: un rechazo no debe dejar un nodo suelto.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            (await db.Set<UnidadOrganizativa>().AsNoTracking()
                .AnyAsync(u => u.Nombre == "Fantasma"))
                .Should().BeFalse();

            (await db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>().AsNoTracking()
                .AnyAsync(e => e.CompaniaId == contratista.Id))
                .Should().BeFalse();
        });
    }

    [Fact]
    public async Task Consultar_el_arbol_de_una_contratista_devuelve_404()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Sin Árbol",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(contratista.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/unidades-organizativas/arbol?companiaPrincipalId={contratista.Id}",
                UriKind.Relative));

        // 404 y no 400: una CONTRATISTA sencillamente no tiene árbol que consultar
        // (contracts/org-units.yaml).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reclasificar_la_compania_a_principal_habilita_la_creacion()
    {
        // Confirma que el rechazo depende del tipo vigente y no de algo fijado en el alta: la misma
        // petición que fallaba pasa tras reclasificar la compañía.
        var compania = await fixture.Api.SembrarCompaniaAsync(
            "Cambia de tipo",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(compania.Id);

        using (var rechazada = await cliente.PostAsJsonAsync(
            new Uri("/api/unidades-organizativas", UriKind.Relative),
            new UnidadOrganizativaRequest("Gerencia", Estado.ACTIVO, null, compania.Id),
            ApiFactory.Json))
        {
            rechazada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var entidad = await db.Set<Compania>().FirstAsync(c => c.Id == compania.Id);
            entidad.TipoCompania = TipoCompania.PRINCIPAL_MANDANTE;
            await db.SaveChangesAsync();
        });

        var raiz = await cliente.CrearRaizAsync("Gerencia", compania.Id);
        raiz.CompaniaPrincipalId.Should().Be(compania.Id);
    }
}
