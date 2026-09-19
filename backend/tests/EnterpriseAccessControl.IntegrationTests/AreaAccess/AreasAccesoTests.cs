using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.AreaAccess;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.AreaAccess;

/// <summary>
/// Creación del árbol de áreas físicas, herencia de la Compañía Principal y rechazo de ciclos
/// (RF-009, RF-038, RF-046, Principio V).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AreasAccesoTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Un_area_raiz_queda_asociada_a_su_compania_principal()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Raíz");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta Concentradora", principal.Id);

        raiz.AreaSuperiorId.Should().BeNull();
        raiz.CompaniaPrincipalId.Should().Be(principal.Id);

        // A diferencia de UnidadOrganizativa, la pertenencia es una columna del propio nodo (RF-046)
        // y no una entidad de enlace: se comprueba sobre la fila, que es donde vive.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var fila = await db.Set<AreaAcceso>().AsNoTracking().SingleAsync(a => a.Id == raiz.Id);

            fila.CompaniaPrincipalId.Should().Be(principal.Id);
            fila.AreaSuperiorId.Should().BeNull();
        });
    }

    [Fact]
    public async Task Un_area_hija_hereda_la_compania_principal_de_su_padre()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Herencia");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var hija = await cliente.CrearHijaAsync("Sector Molienda", raiz.Id);
        var nieta = await cliente.CrearHijaAsync("Sala de Control", hija.Id);

        hija.CompaniaPrincipalId.Should().Be(principal.Id);
        nieta.CompaniaPrincipalId.Should().Be(
            principal.Id, "la herencia se propaga a cualquier profundidad");
    }

    [Fact]
    public async Task El_companiaPrincipalId_enviado_en_un_area_hija_se_ignora()
    {
        // contracts/area-access.yaml: "companiaPrincipalId se ignora si se envía" al crear una hija.
        // Si no se ignorase, dos nodos del mismo árbol podrían declarar Principales distintas.
        var propietaria = await fixture.Api.SembrarCompaniaAsync("Dueña del árbol");
        var otra = await fixture.Api.SembrarCompaniaAsync("Principal ajena");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(propietaria.Id, otra.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", propietaria.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/areas-acceso", UriKind.Relative),
            new AreaAccesoRequest("Sector Intruso", Estado.ACTIVO, raiz.Id, otra.Id),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var hija = await respuesta.Content.ReadFromJsonAsync<AreaAccesoDto>(ApiFactory.Json);
        hija!.CompaniaPrincipalId.Should().Be(propietaria.Id, "manda el padre, no el cuerpo enviado");

        (await cliente.ArbolAreasAsync(otra.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Un_area_raiz_sin_compania_principal_se_rechaza_con_400()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Sin Id");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/areas-acceso", UriKind.Relative),
            new AreaAccesoRequest("Huérfana", Estado.ACTIVO),
            ApiFactory.Json);

        // Sin Principal el área no pertenecería a ningún árbol, y RF-046 exige que pertenezca a una.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("VALIDACION_ENTRADA");
    }

    [Fact]
    public async Task Crear_un_area_bajo_un_padre_inexistente_se_rechaza_con_409()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Padre Fantasma");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/areas-acceso", UriKind.Relative),
            new AreaAccesoRequest("Sector Colgante", Estado.ACTIVO, Guid.CreateVersion7()),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task El_arbol_de_areas_refleja_la_jerarquia_creada()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Árbol");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var molienda = await cliente.CrearHijaAsync("Molienda", raiz.Id);
        var sala = await cliente.CrearHijaAsync("Sala de Control", molienda.Id);

        var arbol = await cliente.ArbolAreasAsync(principal.Id);

        arbol.Should().ContainSingle();
        arbol[0].Id.Should().Be(raiz.Id);
        arbol[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(molienda.Id);
        arbol[0].Hijos[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(sala.Id);
    }

    [Fact]
    public async Task No_se_puede_mover_un_area_bajo_si_misma()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Ciclo 1");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var hija = await cliente.CrearHijaAsync("Molienda", raiz.Id);

        using var respuesta = await cliente.MoverAreaAsync(hija.Id, hija.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("CICLO_JERARQUICO");
    }

    [Fact]
    public async Task No_se_puede_mover_un_area_bajo_su_propio_descendiente()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Ciclo 2");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var molienda = await cliente.CrearHijaAsync("Molienda", raiz.Id);
        var sala = await cliente.CrearHijaAsync("Sala de Control", molienda.Id);

        // Escenario del Independent Test de la Historia 6: mover la raíz bajo el nieto.
        using var respuesta = await cliente.MoverAreaAsync(raiz.Id, sala.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("CICLO_JERARQUICO");

        // El rechazo no deja la jerarquía a medio modificar.
        (await cliente.ObtenerAreaAsync(raiz.Id)).AreaSuperiorId.Should().BeNull();
    }

    [Fact]
    public async Task Mover_un_area_bajo_una_hermana_esta_permitido()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Reubica");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var molienda = await cliente.CrearHijaAsync("Molienda", raiz.Id);
        var chancado = await cliente.CrearHijaAsync("Chancado", raiz.Id);

        using var respuesta = await cliente.MoverAreaAsync(chancado.Id, molienda.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var arbol = await cliente.ArbolAreasAsync(principal.Id);
        arbol[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(molienda.Id);
        arbol[0].Hijos[0].Hijos.Should().ContainSingle().Which.Id.Should().Be(chancado.Id);
    }

    [Fact]
    public async Task Promover_un_area_a_raiz_conserva_su_compania_principal()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Promueve");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var molienda = await cliente.CrearHijaAsync("Molienda", raiz.Id);

        using var respuesta = await cliente.MoverAreaAsync(molienda.Id, null);
        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // El área cambia de posición, no de dueño: sigue en el mismo árbol, ahora como segunda raíz.
        var promovida = await cliente.ObtenerAreaAsync(molienda.Id);
        promovida.AreaSuperiorId.Should().BeNull();
        promovida.CompaniaPrincipalId.Should().Be(principal.Id);

        var arbol = await cliente.ArbolAreasAsync(principal.Id);
        arbol.Should().HaveCount(2);
        arbol.Aplanar().Should().Contain([raiz.Id, molienda.Id]);
    }

    [Fact]
    public async Task Actualizar_un_area_cambia_nombre_y_estado_sin_tocar_la_jerarquia()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Actualiza");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var hija = await cliente.CrearHijaAsync("Nombre viejo", raiz.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/areas-acceso/{hija.Id}", UriKind.Relative),
            new AreaAccesoUpdateRequest("Nombre nuevo", Estado.INACTIVO),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizada = await respuesta.Content.ReadFromJsonAsync<AreaAccesoDto>(ApiFactory.Json);
        actualizada!.Nombre.Should().Be("Nombre nuevo");
        actualizada.Estado.Should().Be(Estado.INACTIVO);
        actualizada.AreaSuperiorId.Should().Be(raiz.Id, "el update no reubica; para eso está /mover");
        actualizada.CompaniaPrincipalId.Should().Be(principal.Id);
    }

    [Fact]
    public async Task El_listado_plano_puede_filtrarse_por_estado()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Filtra");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var activa = await cliente.CrearHijaAsync("Activa", raiz.Id);
        var inactiva = await cliente.CrearHijaAsync("Se desactivará", raiz.Id);

        using (var _ = await cliente.PutAsJsonAsync(
            new Uri($"/api/areas-acceso/{inactiva.Id}", UriKind.Relative),
            new AreaAccesoUpdateRequest("Se desactivará", Estado.INACTIVO),
            ApiFactory.Json))
        {
            // Sólo se necesita el efecto de la actualización.
        }

        var activas = await cliente.ListarAreasAsync(principal.Id, Estado.ACTIVO);

        activas.Select(a => a.Id).Should().Contain([raiz.Id, activa.Id]);
        activas.Select(a => a.Id).Should().NotContain(inactiva.Id);
    }

    [Fact]
    public async Task Una_compania_principal_sin_areas_devuelve_un_arbol_vacio()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Minera Áreas Vacía");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        (await cliente.ArbolAreasAsync(principal.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task No_se_puede_consultar_el_arbol_de_una_compania_fuera_de_alcance()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync("Áreas propias");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Áreas ajenas");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(mia.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/areas-acceso/arbol?companiaPrincipalId={ajena.Id}", UriKind.Relative));

        // Fuera de alcance es indistinguible de inexistente (Principio I, RF-049).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_se_puede_obtener_un_area_de_una_compania_fuera_de_alcance()
    {
        var ajena = await fixture.Api.SembrarCompaniaAsync("Principal ajena con áreas");
        var mia = await fixture.Api.SembrarCompaniaAsync("Principal propia");

        using var duena = await fixture.Api.ClienteDeAreasAsync(ajena.Id);
        var area = await duena.CrearRaizAsync("Planta reservada", ajena.Id);

        using var intruso = await fixture.Api.ClienteDeAreasAsync(mia.Id);

        using var respuesta = await intruso.GetAsync(
            new Uri($"/api/areas-acceso/{area.Id}", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
