using System.Net.Http.Json;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.OrgUnits;

/// <summary>
/// Aislamiento entre Compañías Principales: cada una tiene su propio árbol y no ve el de las otras
/// (RF-043, CS-011).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AislamientoMultiPrincipalTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task El_arbol_de_una_principal_no_incluye_nodos_de_la_otra()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Principal Uno");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Principal Dos");

        // Un único usuario con alcance sobre ambas: así se comprueba que el aislamiento lo produce
        // la pertenencia del árbol y no una simple falta de permisos.
        using var cliente = await fixture.Api.ClienteConAlcanceAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Gerencia P1", p1.Id);
        var hija1 = await cliente.CrearHijaAsync("Operaciones P1", raiz1.Id);

        var raiz2 = await cliente.CrearRaizAsync("Gerencia P2", p2.Id);
        var hija2 = await cliente.CrearHijaAsync("Operaciones P2", raiz2.Id);

        var arbol1 = (await cliente.ArbolAsync(p1.Id)).Aplanar().ToList();
        var arbol2 = (await cliente.ArbolAsync(p2.Id)).Aplanar().ToList();

        arbol1.Should().BeEquivalentTo([raiz1.Id, hija1.Id]);
        arbol2.Should().BeEquivalentTo([raiz2.Id, hija2.Id]);

        arbol1.Should().NotIntersectWith(arbol2);
    }

    [Fact]
    public async Task Una_principal_sin_unidades_devuelve_arbol_vacio_aunque_otra_tenga_arbol()
    {
        var conArbol = await fixture.Api.SembrarCompaniaAsync("Con árbol");
        var sinArbol = await fixture.Api.SembrarCompaniaAsync("Sin árbol");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(conArbol.Id, sinArbol.Id);

        await cliente.CrearRaizAsync("Gerencia", conArbol.Id);

        // Escenario exacto del Independent Test de la Historia 2: la misma consulta con la otra
        // Principal debe devolver vacío, no el árbol de la primera.
        (await cliente.ArbolAsync(sinArbol.Id)).Should().BeEmpty();
        (await cliente.ArbolAsync(conArbol.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task El_listado_plano_tambien_esta_aislado_por_principal()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Plano Uno");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Plano Dos");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Raíz 1", p1.Id);
        var raiz2 = await cliente.CrearRaizAsync("Raíz 2", p2.Id);

        var lista1 = await cliente.GetFromJsonAsync<IReadOnlyList<UnidadOrganizativaDto>>(
            new Uri($"/api/unidades-organizativas?companiaPrincipalId={p1.Id}", UriKind.Relative),
            ApiFactory.Json);

        lista1.Should().NotBeNull();
        lista1!.Select(u => u.Id).Should().BeEquivalentTo([raiz1.Id]);
        lista1.Select(u => u.Id).Should().NotContain(raiz2.Id);
        lista1.Should().OnlyContain(u => u.CompaniaPrincipalId == p1.Id);
    }

    [Fact]
    public async Task Varias_principales_coexisten_cada_una_con_su_propia_raiz()
    {
        // RF-043: el sistema admite varias PRINCIPAL_MANDANTE simultáneas. Se verifica sobre los
        // datos que cada árbol tiene exactamente una fila de enlace y que no se comparten.
        var principales = new List<Compania>();

        for (var i = 0; i < 3; i++)
        {
            principales.Add(await fixture.Api.SembrarCompaniaAsync($"Principal {i}"));
        }

        using var cliente = await fixture.Api.ClienteConAlcanceAsync([.. principales.Select(p => p.Id)]);

        var raices = new List<Guid>();
        foreach (var principal in principales)
        {
            raices.Add((await cliente.CrearRaizAsync($"Raíz de {principal.Nombre}", principal.Id)).Id);
        }

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var enlaces = await db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>()
                .AsNoTracking()
                .Where(e => raices.Contains(e.UnidadOrganizativaRaizId))
                .ToListAsync();

            enlaces.Should().HaveCount(3);
            enlaces.Select(e => e.CompaniaId).Should().OnlyHaveUniqueItems();
            enlaces.Select(e => e.UnidadOrganizativaRaizId).Should().OnlyHaveUniqueItems();
        });
    }

    [Fact]
    public void La_entidad_UnidadOrganizativa_no_referencia_a_Compania()
    {
        // RF-044, verificado sobre el modelo de EF Core y no sólo por convención: añadir un
        // CompañíaId al nodo permitiría que declarase una compañía distinta a la de su árbol, y
        // ninguna restricción declarativa podría impedirlo.
        using var ambito = fixture.Api.Services.CreateScope();
        var db = ambito.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var tipo = db.Model.FindEntityType(typeof(UnidadOrganizativa));
        tipo.Should().NotBeNull();

        var referenciadas = tipo!.GetForeignKeys()
            .Select(fk => fk.PrincipalEntityType.ClrType.Name)
            .ToList();

        // Sólo la autorreferencia padre-hijo.
        referenciadas.Should().BeEquivalentTo([nameof(UnidadOrganizativa)]);

        tipo.GetProperties().Select(p => p.Name).Should().NotContain(
            nombre => nombre.Contains("Compania", StringComparison.Ordinal),
            "la pertenencia vive en CompañíaPrincipalUnidadOrganizativaRaiz (RF-044)");
    }

    [Fact]
    public async Task Un_mismo_arbol_no_puede_pertenecer_a_dos_principales()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Dueña legítima");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Aspirante");

        using var cliente = await fixture.Api.ClienteConAlcanceAsync(p1.Id, p2.Id);

        var raiz = await cliente.CrearRaizAsync("Gerencia", p1.Id);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>().Add(
                new CompaniaPrincipalUnidadOrganizativaRaiz
                {
                    CompaniaId = p2.Id,
                    UnidadOrganizativaRaizId = raiz.Id,
                });

            // Lo impide el índice único sobre la raíz, no sólo el código de aplicación.
            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().ThrowAsync<DbUpdateException>();
        });
    }
}
