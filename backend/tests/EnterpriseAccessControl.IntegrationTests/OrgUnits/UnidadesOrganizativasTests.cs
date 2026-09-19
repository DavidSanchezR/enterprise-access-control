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
/// Creación de la jerarquía organizativa y rechazo de ciclos (RF-007, RF-008, RF-038, Principio V).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class UnidadesOrganizativasTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Una_unidad_raiz_queda_asociada_a_su_compania_principal()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Raíz");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia General", principal.Id);

        raiz.UnidadSuperiorId.Should().BeNull();
        raiz.CompaniaPrincipalId.Should().Be(principal.Id);

        // La asociación vive en la entidad de enlace, no en una columna del nodo (RF-044).
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var enlace = await db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>()
                .AsNoTracking()
                .SingleAsync(e => e.UnidadOrganizativaRaizId == raiz.Id);

            enlace.CompaniaId.Should().Be(principal.Id);
        });
    }

    [Fact]
    public async Task Una_unidad_hija_hereda_la_compania_principal_de_su_padre()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Herencia");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var hija = await cliente.CrearHijaAsync("Operaciones", raiz.Id);
        var nieta = await cliente.CrearHijaAsync("Planta", hija.Id);

        hija.CompaniaPrincipalId.Should().Be(principal.Id);
        nieta.CompaniaPrincipalId.Should().Be(principal.Id, "la herencia se propaga a cualquier profundidad");

        // Ningún nodo hijo genera su propia fila de enlace: sólo la raíz la tiene.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var enlaces = await db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>()
                .AsNoTracking()
                .Where(e => e.CompaniaId == principal.Id)
                .ToListAsync();

            enlaces.Should().ContainSingle().Which.UnidadOrganizativaRaizId.Should().Be(raiz.Id);
        });
    }

    [Fact]
    public async Task Un_nodo_raiz_sin_compania_principal_se_rechaza()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Sin Id");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/unidades-organizativas", UriKind.Relative),
            new UnidadOrganizativaRequest("Huérfana", Estado.ACTIVO),
            ApiFactory.Json);

        // Sin Principal no hay forma de saber a qué árbol pertenece el nodo (RF-045).
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task El_arbol_refleja_la_jerarquia_creada()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Árbol");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var operaciones = await cliente.CrearHijaAsync("Operaciones", raiz.Id);
        var planta = await cliente.CrearHijaAsync("Planta", operaciones.Id);

        var arbol = await cliente.ArbolAsync(principal.Id);

        arbol.Should().ContainSingle();
        arbol[0].Id.Should().Be(raiz.Id);
        arbol[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(operaciones.Id);
        arbol[0].Hijos[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(planta.Id);
    }

    [Fact]
    public async Task No_se_puede_mover_un_nodo_bajo_si_mismo()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Ciclo 1");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var hija = await cliente.CrearHijaAsync("Operaciones", raiz.Id);

        using var respuesta = await cliente.MoverAsync(hija.Id, hija.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("CICLO_JERARQUICO");
    }

    [Fact]
    public async Task No_se_puede_mover_un_nodo_bajo_su_propio_descendiente()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Ciclo 2");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var operaciones = await cliente.CrearHijaAsync("Operaciones", raiz.Id);
        var planta = await cliente.CrearHijaAsync("Planta", operaciones.Id);

        // Mover "Operaciones" bajo su propio nieto desconectaría la rama del árbol (RF-038).
        using var respuesta = await cliente.MoverAsync(operaciones.Id, planta.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("CICLO_JERARQUICO");
    }

    [Fact]
    public async Task Mover_un_nodo_bajo_un_hermano_esta_permitido()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Reubica");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var operaciones = await cliente.CrearHijaAsync("Operaciones", raiz.Id);
        var mantenimiento = await cliente.CrearHijaAsync("Mantenimiento", raiz.Id);

        using var respuesta = await cliente.MoverAsync(mantenimiento.Id, operaciones.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var arbol = await cliente.ArbolAsync(principal.Id);
        arbol[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(operaciones.Id);
        arbol[0].Hijos[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(mantenimiento.Id);
    }

    [Fact]
    public async Task Promover_un_nodo_a_raiz_conserva_su_compania_principal()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Promueve");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var operaciones = await cliente.CrearHijaAsync("Operaciones", raiz.Id);

        using var respuesta = await cliente.MoverAsync(operaciones.Id, null);
        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // contracts/org-units.yaml: "companiaPrincipalId no cambia". Al quedar como raíz de su
        // propio árbol necesita su fila de enlace, o perdería la pertenencia que heredaba.
        var promovida = await cliente.GetFromJsonAsync<UnidadOrganizativaDto>(
            new Uri($"/api/unidades-organizativas/{operaciones.Id}", UriKind.Relative),
            ApiFactory.Json);

        promovida!.UnidadSuperiorId.Should().BeNull();
        promovida.CompaniaPrincipalId.Should().Be(principal.Id);

        // Y el árbol de la Principal pasa a tener dos raíces.
        var arbol = await cliente.ArbolAsync(principal.Id);
        arbol.Should().HaveCount(2);
        arbol.Aplanar().Should().Contain([raiz.Id, operaciones.Id]);
    }

    [Fact]
    public async Task Actualizar_una_unidad_cambia_nombre_y_estado_sin_tocar_la_jerarquia()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Actualiza");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var hija = await cliente.CrearHijaAsync("Nombre viejo", raiz.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/unidades-organizativas/{hija.Id}", UriKind.Relative),
            new UnidadOrganizativaUpdateRequest("Nombre nuevo", Estado.INACTIVO),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizada = await respuesta.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(ApiFactory.Json);
        actualizada!.Nombre.Should().Be("Nombre nuevo");
        actualizada.Estado.Should().Be(Estado.INACTIVO);
        actualizada.UnidadSuperiorId.Should().Be(raiz.Id, "el update no reubica; para eso está /mover");
    }

    [Fact]
    public async Task El_listado_plano_puede_filtrarse_por_estado()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Filtra");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var activa = await cliente.CrearHijaAsync("Activa", raiz.Id);
        var inactiva = await cliente.CrearHijaAsync("Se desactivará", raiz.Id);

        using (var _ = await cliente.PutAsJsonAsync(
            new Uri($"/api/unidades-organizativas/{inactiva.Id}", UriKind.Relative),
            new UnidadOrganizativaUpdateRequest("Se desactivará", Estado.INACTIVO),
            ApiFactory.Json))
        {
            // Sólo se necesita el efecto de la actualización.
        }

        var activas = await cliente.GetFromJsonAsync<IReadOnlyList<UnidadOrganizativaDto>>(
            new Uri($"/api/unidades-organizativas?companiaPrincipalId={principal.Id}&estado=ACTIVO",
                UriKind.Relative),
            ApiFactory.Json);

        activas.Should().NotBeNull();
        activas!.Select(u => u.Id).Should().Contain([raiz.Id, activa.Id]);
        activas.Select(u => u.Id).Should().NotContain(inactiva.Id);
    }

    [Fact]
    public async Task Una_compania_principal_sin_unidades_devuelve_un_arbol_vacio()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Vacía");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var arbol = await cliente.ArbolAsync(principal.Id);

        // Vacío, no error: una Principal recién creada aún no tiene estructura.
        arbol.Should().BeEmpty();
    }

    [Fact]
    public async Task No_se_puede_consultar_el_arbol_de_una_compania_fuera_de_alcance()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(mia.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/unidades-organizativas/arbol?companiaPrincipalId={ajena.Id}",
                UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
