using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// La búsqueda de personas queda limitada al alcance de compañías del usuario (RF-035, RF-005).
/// </summary>
/// <remarks>
/// La pertenencia de la persona a una compañía es lo que la hace visible. Es la regla que impide que
/// el administrador de una contratista vea el padrón de otra, y se comprueba tanto en el listado como
/// en el acceso directo por identificador.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PersonasBusquedaTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string Sufijo() => Guid.CreateVersion7().ToString("N")[..12];

    private async Task<HttpClient> ClienteConAlcanceAsync(params Guid[] companias)
    {
        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"busqueda.{Guid.CreateVersion7():N}@empresa.cl",
            Password,
            alcanceCompanias: companias);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    private static async Task<PaginaResponse<PersonaDto>> BuscarAsync(
        HttpClient cliente,
        string consulta = "")
    {
        var pagina = await cliente.GetFromJsonAsync<PaginaResponse<PersonaDto>>(
            new Uri($"/api/personas{consulta}", UriKind.Relative), ApiFactory.Json);

        pagina.Should().NotBeNull();
        return pagina!;
    }

    [Fact]
    public async Task Solo_se_ven_personas_de_companias_dentro_del_alcance()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync($"Propia {Sufijo()}");
        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {Sufijo()}");

        var visible = await fixture.Api.SembrarPersonaAsync(companiaId: mia.Id, apellidos: "Visible");
        var oculta = await fixture.Api.SembrarPersonaAsync(companiaId: ajena.Id, apellidos: "Oculta");

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        var pagina = await BuscarAsync(cliente);

        pagina.Items.Select(p => p.Id).Should().Contain(visible.Id);
        pagina.Items.Select(p => p.Id).Should().NotContain(oculta.Id);
    }

    [Fact]
    public async Task Consultar_por_id_una_persona_fuera_de_alcance_devuelve_404()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync($"Propia {Sufijo()}");
        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {Sufijo()}");

        var oculta = await fixture.Api.SembrarPersonaAsync(companiaId: ajena.Id);

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/personas/{oculta.Id}", UriKind.Relative));

        // 404 y no 403: un 403 confirmaría que esa persona existe y permitiría enumerar el padrón
        // ajeno probando identificadores (Principio I).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_se_puede_actualizar_una_persona_fuera_de_alcance()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync($"Propia {Sufijo()}");
        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {Sufijo()}");

        var oculta = await fixture.Api.SembrarPersonaAsync(companiaId: ajena.Id);

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/personas/{oculta.Id}", UriKind.Relative),
            new PersonaRequest(
                "Intruso", "Editor", new DateOnly(1990, 1, 1),
                ApiFactory.Maestros.Dni, oculta.NumeroDocumento,
                ApiFactory.Maestros.Masculino, "intruso@empresa.cl",
                ApiFactory.Maestros.OPositivo, "X", "1"),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Una_persona_sin_pertenencia_vigente_no_aparece_en_ninguna_busqueda()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Sin vínculo {Sufijo()}");

        // Alta sin pertenencia: la persona existe pero todavía no está contratada por nadie.
        var suelta = await fixture.Api.SembrarPersonaAsync();

        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        var pagina = await BuscarAsync(cliente);

        pagina.Items.Select(p => p.Id).Should().NotContain(suelta.Id);
    }

    [Fact]
    public async Task Una_pertenencia_expirada_deja_de_hacer_visible_a_la_persona()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Expirada {Sufijo()}");

        var exEmpleada = await fixture.Api.SembrarPersonaAsync(
            companiaId: compania.Id,
            inicioVigencia: DateTime.UtcNow.AddYears(-2),
            finVigencia: DateTime.UtcNow.AddDays(-1));

        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        var pagina = await BuscarAsync(cliente);

        // La vigencia se evalúa dinámicamente contra las fechas, no por un campo de estado
        // (Principio IV): una pertenencia vencida deja de conceder visibilidad sin que nadie
        // ejecute ninguna acción.
        pagina.Items.Select(p => p.Id).Should().NotContain(exEmpleada.Id);
    }

    [Fact]
    public async Task Una_pertenencia_futura_todavia_no_hace_visible_a_la_persona()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Futura {Sufijo()}");

        var porIngresar = await fixture.Api.SembrarPersonaAsync(
            companiaId: compania.Id,
            inicioVigencia: DateTime.UtcNow.AddDays(30),
            finVigencia: DateTime.UtcNow.AddYears(1));

        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        var pagina = await BuscarAsync(cliente);

        pagina.Items.Select(p => p.Id).Should().NotContain(porIngresar.Id);
    }

    [Fact]
    public async Task El_filtro_por_compania_se_interseca_con_el_alcance()
    {
        var a = await fixture.Api.SembrarCompaniaAsync($"A {Sufijo()}");
        var b = await fixture.Api.SembrarCompaniaAsync($"B {Sufijo()}");
        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {Sufijo()}");

        var deA = await fixture.Api.SembrarPersonaAsync(companiaId: a.Id);
        var deB = await fixture.Api.SembrarPersonaAsync(companiaId: b.Id);
        var deAjena = await fixture.Api.SembrarPersonaAsync(companiaId: ajena.Id);

        using var cliente = await ClienteConAlcanceAsync(a.Id, b.Id);

        var soloA = await BuscarAsync(cliente, $"?companiaId={a.Id}");
        soloA.Items.Select(p => p.Id).Should().Contain(deA.Id);
        soloA.Items.Select(p => p.Id).Should().NotContain(deB.Id);

        // Filtrar por una compañía fuera del alcance no es error: devuelve vacío. Un 403 revelaría
        // que esa compañía existe.
        var fueraDeAlcance = await BuscarAsync(cliente, $"?companiaId={ajena.Id}");
        fueraDeAlcance.Items.Should().BeEmpty();
        fueraDeAlcance.Total.Should().Be(0);
        fueraDeAlcance.Items.Select(p => p.Id).Should().NotContain(deAjena.Id);
    }

    [Fact]
    public async Task La_busqueda_por_texto_cubre_nombres_apellidos_y_documento()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Texto {Sufijo()}");
        var marca = Sufijo();

        var persona = await fixture.Api.SembrarPersonaAsync(
            numeroDocumento: marca,
            companiaId: compania.Id,
            apellidos: $"Quispe{marca}",
            nombres: $"Rosa{marca}");

        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        foreach (var termino in new[] { $"Quispe{marca}", $"Rosa{marca}", marca })
        {
            var pagina = await BuscarAsync(cliente, $"?texto={termino}");

            pagina.Items.Should().ContainSingle("buscando por '{0}'", termino)
                .Which.Id.Should().Be(persona.Id);
        }
    }

    [Fact]
    public async Task La_busqueda_por_texto_sigue_respetando_el_alcance()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync($"Propia {Sufijo()}");
        var ajena = await fixture.Api.SembrarCompaniaAsync($"Ajena {Sufijo()}");
        var marca = Sufijo();

        await fixture.Api.SembrarPersonaAsync(companiaId: ajena.Id, apellidos: $"Oculta{marca}");

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        var pagina = await BuscarAsync(cliente, $"?texto={marca}");

        // El filtro de texto nunca puede ensanchar lo que el alcance ya restringió.
        pagina.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_usuario_sin_alcance_no_ve_ninguna_persona()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Cualquiera {Sufijo()}");
        await fixture.Api.SembrarPersonaAsync(companiaId: compania.Id);

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"sinalcance.{Guid.CreateVersion7():N}@empresa.cl",
            Password);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.GetAsync(new Uri("/api/personas", UriKind.Relative));

        // La política CompaniaScope lo detiene antes de llegar al servicio: denegación por defecto.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_listado_pagina_los_resultados()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Paginada {Sufijo()}");
        var marca = Sufijo();

        for (var i = 0; i < 5; i++)
        {
            await fixture.Api.SembrarPersonaAsync(
                companiaId: compania.Id, apellidos: $"Pagina{marca}{i:D2}");
        }

        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        var primera = await BuscarAsync(cliente, $"?texto={marca}&pagina=1&tamañoPagina=2");

        primera.Items.Should().HaveCount(2);
        primera.Total.Should().Be(5);
        primera.Pagina.Should().Be(1);

        var tercera = await BuscarAsync(cliente, $"?texto={marca}&pagina=3&tamañoPagina=2");
        tercera.Items.Should().ContainSingle();
    }
}
