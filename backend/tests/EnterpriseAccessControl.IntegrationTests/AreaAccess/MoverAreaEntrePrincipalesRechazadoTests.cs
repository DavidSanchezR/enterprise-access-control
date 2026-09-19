using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.AreaAccess;

/// <summary>
/// Un área no puede reubicarse en el árbol de otra Compañía Principal (RF-046).
/// </summary>
/// <remarks>
/// El movimiento no crea ningún ciclo —son árboles disjuntos—, así que la comprobación de ciclos no
/// lo detendría. Lo impide una regla distinta: cambiaría el <c>companiaPrincipalId</c> del área, que
/// aquí es un dato propio del nodo y no es editable.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class MoverAreaEntrePrincipalesRechazadoTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Mover_un_area_al_arbol_de_otra_principal_se_rechaza_con_409()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Áreas Origen");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Áreas Destino");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Planta P1", p1.Id);
        var hija1 = await cliente.CrearHijaAsync("Molienda P1", raiz1.Id);

        var raiz2 = await cliente.CrearRaizAsync("Planta P2", p2.Id);

        using var respuesta = await cliente.MoverAreaAsync(hija1.Id, raiz2.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");
    }

    [Fact]
    public async Task El_rechazo_deja_la_jerarquia_de_areas_intacta()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Áreas Origen Intacto");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Áreas Destino Intacto");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Planta P1", p1.Id);
        var hija1 = await cliente.CrearHijaAsync("Molienda P1", raiz1.Id);
        var raiz2 = await cliente.CrearRaizAsync("Planta P2", p2.Id);

        using (var rechazado = await cliente.MoverAreaAsync(hija1.Id, raiz2.Id))
        {
            rechazado.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        // El área sigue colgando de su padre original y en su Principal original.
        var despues = await cliente.ObtenerAreaAsync(hija1.Id);

        despues.AreaSuperiorId.Should().Be(raiz1.Id);
        despues.CompaniaPrincipalId.Should().Be(p1.Id);

        // Y el árbol de destino no ganó ningún nodo.
        (await cliente.ArbolAreasAsync(p2.Id)).Aplanar().Should().BeEquivalentTo([raiz2.Id]);
    }

    [Fact]
    public async Task Mover_un_area_raiz_al_arbol_de_otra_principal_tambien_se_rechaza()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Áreas Raíz Origen");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Áreas Raíz Destino");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Planta P1", p1.Id);
        var raiz2 = await cliente.CrearRaizAsync("Planta P2", p2.Id);

        // Caso especialmente delicado: absorber una raíz en otro árbol arrastraría con ella todo su
        // subárbol, que seguiría declarando la Principal antigua en cada nodo.
        using var respuesta = await cliente.MoverAreaAsync(raiz1.Id, raiz2.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await cliente.ArbolAreasAsync(p1.Id)).Aplanar().Should().BeEquivalentTo([raiz1.Id]);
    }

    [Fact]
    public async Task Mover_un_area_a_un_arbol_fuera_de_alcance_se_rechaza_sin_revelarlo()
    {
        // Si el destino ni siquiera está en el alcance del usuario, el rechazo no debe distinguirse
        // del caso anterior: confirmar que el árbol ajeno existe ya sería una filtración (Principio I).
        var mia = await fixture.Api.SembrarCompaniaAsync("Áreas propias mover");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Áreas ajenas mover");

        using var duena = await fixture.Api.ClienteDeAreasAsync(ajena.Id);
        var raizAjena = await duena.CrearRaizAsync("Planta ajena", ajena.Id);

        using var cliente = await fixture.Api.ClienteDeAreasAsync(mia.Id);
        var raizPropia = await cliente.CrearRaizAsync("Planta propia", mia.Id);
        var hija = await cliente.CrearHijaAsync("Molienda propia", raizPropia.Id);

        using var respuesta = await cliente.MoverAreaAsync(hija.Id, raizAjena.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");

        (await cliente.ObtenerAreaAsync(hija.Id)).AreaSuperiorId.Should().Be(raizPropia.Id);
    }

    [Fact]
    public async Task Mover_dentro_del_mismo_arbol_de_areas_sigue_permitido()
    {
        // Contrapunto: la regla restringe el cruce entre Principales, no la reorganización interna.
        var principal = await fixture.Api.SembrarCompaniaAsync("Áreas Reorganiza");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var a = await cliente.CrearHijaAsync("Sector A", raiz.Id);
        var b = await cliente.CrearHijaAsync("Sector B", raiz.Id);

        using var respuesta = await cliente.MoverAreaAsync(b.Id, a.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task No_se_puede_mover_un_area_bajo_un_padre_inexistente()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Áreas Padre Inexistente");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var hija = await cliente.CrearHijaAsync("Molienda", raiz.Id);

        using var respuesta = await cliente.MoverAreaAsync(hija.Id, Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
