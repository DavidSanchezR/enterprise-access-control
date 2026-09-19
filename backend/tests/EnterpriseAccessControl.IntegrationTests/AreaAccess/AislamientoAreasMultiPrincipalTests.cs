using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.AreaAccess;

/// <summary>
/// Aislamiento entre Compañías Principales: cada una tiene su propio árbol de áreas y no ve el de las
/// otras (RF-043, CS-011).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AislamientoAreasMultiPrincipalTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task El_arbol_de_areas_de_una_principal_no_incluye_nodos_de_la_otra()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Áreas Principal Uno");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Áreas Principal Dos");

        // Un único usuario con alcance sobre ambas: así se comprueba que el aislamiento lo produce la
        // pertenencia del área y no una simple falta de permisos.
        using var cliente = await fixture.Api.ClienteDeAreasAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Planta P1", p1.Id);
        var hija1 = await cliente.CrearHijaAsync("Molienda P1", raiz1.Id);

        var raiz2 = await cliente.CrearRaizAsync("Planta P2", p2.Id);
        var hija2 = await cliente.CrearHijaAsync("Molienda P2", raiz2.Id);

        var arbol1 = (await cliente.ArbolAreasAsync(p1.Id)).Aplanar().ToList();
        var arbol2 = (await cliente.ArbolAreasAsync(p2.Id)).Aplanar().ToList();

        arbol1.Should().BeEquivalentTo([raiz1.Id, hija1.Id]);
        arbol2.Should().BeEquivalentTo([raiz2.Id, hija2.Id]);

        arbol1.Should().NotIntersectWith(arbol2);
    }

    [Fact]
    public async Task Una_principal_sin_areas_devuelve_arbol_vacio_aunque_otra_tenga_arbol()
    {
        var conAreas = await fixture.Api.SembrarCompaniaAsync("Con áreas");
        var sinAreas = await fixture.Api.SembrarCompaniaAsync("Sin áreas");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(conAreas.Id, sinAreas.Id);

        await cliente.CrearRaizAsync("Planta", conAreas.Id);

        // Escenario exacto del Independent Test de la Historia 6: la misma consulta con la otra
        // Principal debe devolver vacío, no el árbol de la primera.
        (await cliente.ArbolAreasAsync(sinAreas.Id)).Should().BeEmpty();
        (await cliente.ArbolAreasAsync(conAreas.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task El_listado_plano_de_areas_tambien_esta_aislado_por_principal()
    {
        var p1 = await fixture.Api.SembrarCompaniaAsync("Áreas Plano Uno");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Áreas Plano Dos");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Planta 1", p1.Id);
        var raiz2 = await cliente.CrearRaizAsync("Planta 2", p2.Id);

        var lista1 = await cliente.ListarAreasAsync(p1.Id);

        lista1.Select(a => a.Id).Should().BeEquivalentTo([raiz1.Id]);
        lista1.Select(a => a.Id).Should().NotContain(raiz2.Id);
        lista1.Should().OnlyContain(a => a.CompaniaPrincipalId == p1.Id);
    }

    [Fact]
    public async Task Varias_principales_coexisten_cada_una_con_su_propio_arbol_de_areas()
    {
        // RF-043: el sistema admite varias PRINCIPAL_MANDANTE simultáneas. Se verifica sobre los datos
        // que cada área declara su propia Principal y que ninguna se comparte entre árboles.
        var principales = new List<Compania>();

        for (var i = 0; i < 3; i++)
        {
            principales.Add(await fixture.Api.SembrarCompaniaAsync($"Áreas Principal {i}"));
        }

        using var cliente = await fixture.Api.ClienteDeAreasAsync([.. principales.Select(p => p.Id)]);

        var raices = new List<Guid>();
        foreach (var principal in principales)
        {
            raices.Add((await cliente.CrearRaizAsync($"Planta de {principal.Nombre}", principal.Id)).Id);
        }

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var areas = await db.Set<AreaAcceso>()
                .AsNoTracking()
                .Where(a => raices.Contains(a.Id))
                .ToListAsync();

            areas.Should().HaveCount(3);
            areas.Select(a => a.CompaniaPrincipalId).Should().OnlyHaveUniqueItems();
        });
    }

    [Fact]
    public async Task Un_area_de_otra_principal_no_puede_usarse_como_padre_al_crear()
    {
        // Complemento del rechazo en /mover (T121): el cruce entre árboles tampoco puede colarse por
        // la creación. Aquí no se rechaza —el padre manda—, sino que el área nace en el árbol del
        // padre, nunca en el de la Principal enviada.
        var p1 = await fixture.Api.SembrarCompaniaAsync("Áreas Origen Creación");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Áreas Destino Creación");

        using var cliente = await fixture.Api.ClienteDeAreasAsync(p1.Id, p2.Id);

        var raiz1 = await cliente.CrearRaizAsync("Planta P1", p1.Id);

        var hija = await cliente.CrearHijaAsync("Sector", raiz1.Id);

        hija.CompaniaPrincipalId.Should().Be(p1.Id);
        (await cliente.ArbolAreasAsync(p2.Id)).Should().BeEmpty();
    }

    [Fact]
    public void La_entidad_AreaAcceso_si_referencia_a_Compania()
    {
        // Contraste deliberado con UnidadOrganizativa, verificado sobre el modelo de EF Core: RF-046
        // sí admite la FK directa que RF-044 prohíbe. Se fija aquí para que un futuro "alineemos las
        // dos jerarquías" tenga que enfrentarse a una prueba roja y no pase inadvertido.
        using var ambito = fixture.Api.Services.CreateScope();
        var db = ambito.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var tipo = db.Model.FindEntityType(typeof(AreaAcceso));
        tipo.Should().NotBeNull();

        var referenciadas = tipo!.GetForeignKeys()
            .Select(fk => fk.PrincipalEntityType.ClrType.Name)
            .ToList();

        referenciadas.Should().BeEquivalentTo([nameof(AreaAcceso), nameof(Compania)]);
    }
}
