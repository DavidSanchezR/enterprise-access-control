using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.AreaAccess;

/// <summary>
/// Una compañía CONTRATISTA no puede poseer áreas de acceso (RF-046, Historia 6 criterio 5).
/// </summary>
/// <remarks>
/// Las áreas son espacios físicos del recinto de la Principal: una contratista trabaja en ellos, no
/// los posee. Por eso la FK directa de <c>AreaAcceso</c> no basta por sí sola —apunta a Compañía sin
/// distinguir el tipo— y la clasificación se comprueba en el servicio.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AreaRaizContratistaRechazadaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Crear_un_area_raiz_sobre_una_contratista_se_rechaza_con_400()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Áreas Servicios",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteDeAreasAsync(contratista.Id);

        using var respuesta = await cliente.PostRaizAsync("Planta", contratista.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");
    }

    [Fact]
    public async Task El_rechazo_no_deja_ningun_area_creada()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Áreas Limpia",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteDeAreasAsync(contratista.Id);

        using var respuesta = await cliente.PostRaizAsync("Área Fantasma", contratista.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // La validación ocurre antes de escribir nada: un rechazo no debe dejar un nodo suelto.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            (await db.Set<AreaAcceso>().AsNoTracking()
                .AnyAsync(a => a.CompaniaPrincipalId == contratista.Id))
                .Should().BeFalse();

            (await db.Set<AreaAcceso>().AsNoTracking()
                .AnyAsync(a => a.Nombre == "Área Fantasma"))
                .Should().BeFalse();
        });
    }

    [Fact]
    public async Task Consultar_el_arbol_de_areas_de_una_contratista_devuelve_404()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Sin Áreas",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteDeAreasAsync(contratista.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/areas-acceso/arbol?companiaPrincipalId={contratista.Id}",
                UriKind.Relative));

        // 404 y no 400: una CONTRATISTA sencillamente no tiene árbol de áreas que consultar
        // (contracts/area-access.yaml).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listar_las_areas_de_una_contratista_devuelve_404()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Lista Áreas",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteDeAreasAsync(contratista.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/areas-acceso?companiaPrincipalId={contratista.Id}", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reclasificar_la_compania_a_principal_habilita_la_creacion_de_areas()
    {
        // Confirma que el rechazo depende del tipo vigente y no de algo fijado en el alta: la misma
        // petición que fallaba pasa tras reclasificar la compañía.
        var compania = await fixture.Api.SembrarCompaniaAsync(
            "Áreas Cambia de tipo",
            TipoCompania.CONTRATISTA);

        using var cliente = await fixture.Api.ClienteDeAreasAsync(compania.Id);

        using (var rechazada = await cliente.PostRaizAsync("Planta", compania.Id))
        {
            rechazada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var entidad = await db.Set<Compania>().FirstAsync(c => c.Id == compania.Id);
            entidad.TipoCompania = TipoCompania.PRINCIPAL_MANDANTE;
            await db.SaveChangesAsync();
        });

        var raiz = await cliente.CrearRaizAsync("Planta", compania.Id);
        raiz.CompaniaPrincipalId.Should().Be(compania.Id);
    }
}
