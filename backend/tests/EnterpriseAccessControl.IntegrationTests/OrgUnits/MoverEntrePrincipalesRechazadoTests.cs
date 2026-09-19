using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAccessControl.IntegrationTests.OrgUnits;

/// <summary>
/// Un nodo no puede reubicarse en el árbol de otra Compañía Principal (RF-045).
/// </summary>
/// <remarks>
/// El movimiento no crea ningún ciclo —son árboles disjuntos—, así que la comprobación de ciclos no
/// lo detendría. Lo que lo impide es una regla distinta: cambiaría implícitamente la Compañía
/// Principal propietaria del nodo, que no es editable.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class MoverEntrePrincipalesRechazadoTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Mover_un_nodo_al_arbol_de_otra_principal_se_rechaza_con_409()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Principal Origen");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Principal Destino");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Gerencia P1", p1.Id);
        var hija1 = await cliente.CrearHijaAsync("Operaciones P1", raiz1.Id);

        var raiz2 = await cliente.CrearRaizAsync("Gerencia P2", p2.Id);

        using var respuesta = await cliente.MoverAsync(hija1.Id, raiz2.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("COMPANIA_DEBE_SER_PRINCIPAL");
    }

    [Fact]
    public async Task El_rechazo_deja_la_jerarquia_intacta()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Origen Intacto");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Destino Intacto");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Gerencia P1", p1.Id);
        var hija1 = await cliente.CrearHijaAsync("Operaciones P1", raiz1.Id);
        var raiz2 = await cliente.CrearRaizAsync("Gerencia P2", p2.Id);

        using (var rechazado = await cliente.MoverAsync(hija1.Id, raiz2.Id))
        {
            rechazado.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        // El nodo sigue colgando de su padre original y en su Principal original.
        var despues = await cliente.GetFromJsonAsync<UnidadOrganizativaDto>(
            new Uri($"/api/unidades-organizativas/{hija1.Id}", UriKind.Relative),
            ApiFactory.Json);

        despues!.UnidadSuperiorId.Should().Be(raiz1.Id);
        despues.CompaniaPrincipalId.Should().Be(p1.Id);

        // Y el árbol de destino no ganó ningún nodo.
        (await cliente.ArbolAsync(p2.Id)).Aplanar().Should().BeEquivalentTo([raiz2.Id]);
    }

    [Fact]
    public async Task Mover_una_raiz_al_arbol_de_otra_principal_tambien_se_rechaza()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Raíz Origen");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Raíz Destino");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Gerencia P1", p1.Id);
        var raiz2 = await cliente.CrearRaizAsync("Gerencia P2", p2.Id);

        // Caso especialmente delicado: absorber una raíz en otro árbol arrastraría con ella todo su
        // subárbol y su fila de enlace.
        using var respuesta = await cliente.MoverAsync(raiz1.Id, raiz2.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await cliente.ArbolAsync(p1.Id)).Aplanar().Should().BeEquivalentTo([raiz1.Id]);
    }

    [Fact]
    public async Task Mover_dentro_del_mismo_arbol_sigue_permitido()
    {
        // Contrapunto: la regla restringe el cruce entre Principales, no la reorganización interna.
        var principal = await fixture.Api.SembrarCompaniaAsync("Reorganiza");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var a = await cliente.CrearHijaAsync("Área A", raiz.Id);
        var b = await cliente.CrearHijaAsync("Área B", raiz.Id);

        using var respuesta = await cliente.MoverAsync(b.Id, a.Id);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task No_se_puede_mover_un_nodo_bajo_un_padre_inexistente()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Padre Inexistente");
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", principal.Id);
        var hija = await cliente.CrearHijaAsync("Operaciones", raiz.Id);

        using var respuesta = await cliente.MoverAsync(hija.Id, Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
