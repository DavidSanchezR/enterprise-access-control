using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// Búsqueda server-side de usuarios dentro del alcance autorizado (RF-077, UX-22).
/// </summary>
/// <remarks>
/// Cierra la desviación D-4: hasta la Sesión 2026-09-20 el filtro por correo vivía en el cliente y solo
/// alcanzaba a la página ya recibida, de modo que buscar a alguien que caía en la página 2 devolvía
/// "sin resultados" sin avisarlo. Lo que estas pruebas fijan es el **orden de composición** —alcance →
/// filtros → total → página—: si alguna vez se invirtiera, volviendo a paginar antes de buscar, la
/// primera prueba falla.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class BusquedaUsuariosTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    /// <summary>Página pequeña a propósito: así "estar en otra página" es fácil de construir.</summary>
    private const int TamanoPagina = 3;

    private static string CorreoCon(string marca) =>
        $"busqueda.{marca}.{Guid.NewGuid():N}@empresa.cl";

    /// <summary>
    /// Discriminador aleatorio para las búsquedas.
    /// </summary>
    /// <remarks>
    /// Deliberadamente <see cref="Guid.NewGuid"/> (v4, aleatorio) y no <c>CreateVersion7</c>: un GUID v7
    /// empieza por su marca de tiempo, así que los primeros caracteres de dos GUID creados en el mismo
    /// instante coinciden — y una búsqueda por ese prefijo encontraría también a los usuarios que otras
    /// pruebas sembraron a la vez en la base compartida del contenedor.
    /// </remarks>
    private static string Marca() => Guid.NewGuid().ToString("N")[..12];

    private static Uri Ruta(string consulta) => new($"/api/usuarios?{consulta}", UriKind.Relative);

    [Fact]
    public async Task Encuentra_un_usuario_que_no_esta_en_la_primera_pagina()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Busqueda Paginada");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("aaa-admin"), Password, alcanceCompanias: [compania.Id]);

        // Correos que ordenan ANTES del buscado, para empujarlo fuera de la primera página: el
        // listado ordena por correo ascendente.
        for (var i = 0; i < TamanoPagina + 2; i++)
        {
            await fixture.Api.SembrarUsuarioAsync(
                CorreoCon($"bbb-relleno{i}"), Password, alcanceCompanias: [compania.Id]);
        }

        var marca = Marca();
        var buscado = await fixture.Api.SembrarUsuarioAsync(
            $"zzz.{marca}@empresa.cl", Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        // Primero se confirma la premisa: sin búsqueda, el buscado NO está en la página 1.
        var primeraPagina = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"pagina=1&tamañoPagina={TamanoPagina}"), ApiFactory.Json);

        primeraPagina!.Items.Should().NotContain(u => u.Id == buscado.Id,
            "la prueba solo tiene sentido si el usuario buscado cae fuera de la primera página");

        // Y ahora, con búsqueda, aparece en la página 1 del resultado filtrado.
        var conBusqueda = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca}&pagina=1&tamañoPagina={TamanoPagina}"), ApiFactory.Json);

        conBusqueda!.Items.Should().ContainSingle().Which.Id.Should().Be(buscado.Id);

        // El total cuenta las coincidencias, no el universo del sistema.
        conBusqueda.Total.Should().Be(1);
    }

    [Fact]
    public async Task Busca_por_coincidencia_parcial_y_por_correo_exacto()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Busqueda Parcial");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("admin"), Password, alcanceCompanias: [compania.Id]);

        var marca = Marca();
        var buscado = await fixture.Api.SembrarUsuarioAsync(
            $"parcial.{marca}@empresa.cl", Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        // Fragmento del medio del correo: la coincidencia es parcial, no por prefijo.
        var parcial = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca[2..8]}"), ApiFactory.Json);

        parcial!.Items.Should().ContainSingle().Which.Id.Should().Be(buscado.Id);

        var exacto = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={Uri.EscapeDataString(buscado.Correo)}"), ApiFactory.Json);

        exacto!.Items.Should().ContainSingle().Which.Id.Should().Be(buscado.Id);
    }

    [Fact]
    public async Task Combina_texto_y_estado_con_AND()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Texto Y Estado");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("admin"), Password, alcanceCompanias: [compania.Id]);

        var marca = Marca();

        var activo = await fixture.Api.SembrarUsuarioAsync(
            $"activo.{marca}@empresa.cl", Password, alcanceCompanias: [compania.Id]);

        var inactivo = await fixture.Api.SembrarUsuarioAsync(
            $"inactivo.{marca}@empresa.cl",
            Password,
            estado: EstadoUsuario.INACTIVO,
            alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        // El texto solo alcanza a los dos; el estado recorta a uno. Se aplican ambos, no uno u otro.
        var soloTexto = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca}"), ApiFactory.Json);

        soloTexto!.Items.Select(u => u.Id).Should().BeEquivalentTo([activo.Id, inactivo.Id]);

        var conEstado = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca}&estado={nameof(EstadoUsuario.ACTIVO)}"), ApiFactory.Json);

        conEstado!.Items.Should().ContainSingle().Which.Id.Should().Be(activo.Id);
        conEstado.Total.Should().Be(1);
    }

    [Fact]
    public async Task Sin_coincidencias_responde_200_con_pagina_vacia_y_nunca_404()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Sin Coincidencias");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("admin"), Password, alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.GetAsync(
            Ruta($"texto=inexistente-{Guid.CreateVersion7():N}"));

        // 404 está reservado a "recurso fuera de alcance" (RF-077): una búsqueda vacía es un 200.
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaDto>(ApiFactory.Json);

        pagina!.Items.Should().BeEmpty();
        pagina.Total.Should().Be(0);
    }

    [Fact]
    public async Task Buscar_el_correo_exacto_de_un_usuario_ajeno_no_revela_su_existencia()
    {
        var propia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("admin"), Password, alcanceCompanias: [propia.Id]);

        var ajeno = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("objetivo"), Password, alcanceCompanias: [ajena.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        // Se conoce el correo COMPLETO y es válido. La búsqueda no debe convertirse en un oráculo de
        // enumeración: el resultado tiene que ser idéntico al de un correo que no existe.
        var conCorreoAjeno = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={Uri.EscapeDataString(ajeno.Correo)}"), ApiFactory.Json);

        var conCorreoInexistente = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto=nadie-{Guid.CreateVersion7():N}@empresa.cl"), ApiFactory.Json);

        conCorreoAjeno!.Items.Should().BeEmpty();
        conCorreoAjeno.Total.Should().Be(0);
        conCorreoAjeno.Total.Should().Be(conCorreoInexistente!.Total);
    }

    [Fact]
    public async Task El_administrador_global_busca_en_todas_las_companias()
    {
        var primera = await fixture.Api.SembrarCompaniaAsync("Global Busca 1");
        var segunda = await fixture.Api.SembrarCompaniaAsync("Global Busca 2");

        var marca = Marca();

        var deLaPrimera = await fixture.Api.SembrarUsuarioAsync(
            $"p1.{marca}@empresa.cl", Password, alcanceCompanias: [primera.Id]);

        var deLaSegunda = await fixture.Api.SembrarUsuarioAsync(
            $"p2.{marca}@empresa.cl", Password, alcanceCompanias: [segunda.Id]);

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("global"), Password, global: true);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo);

        var pagina = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca}"), ApiFactory.Json);

        // El alcance GLOBAL no enumera compañías: la búsqueda alcanza a las dos sin listarlas.
        pagina!.Items.Select(u => u.Id).Should().BeEquivalentTo([deLaPrimera.Id, deLaSegunda.Id]);
    }

    [Fact]
    public async Task La_busqueda_respeta_la_paginacion_del_resultado_filtrado()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Busqueda Y Paginas");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoCon("admin"), Password, alcanceCompanias: [compania.Id]);

        var marca = Marca();

        for (var i = 0; i < 5; i++)
        {
            await fixture.Api.SembrarUsuarioAsync(
                $"lote{i}.{marca}@empresa.cl", Password, alcanceCompanias: [compania.Id]);
        }

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        var primera = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca}&pagina=1&tamañoPagina=2"), ApiFactory.Json);

        var segunda = await cliente.GetFromJsonAsync<PaginaDto>(
            Ruta($"texto={marca}&pagina=2&tamañoPagina=2"), ApiFactory.Json);

        primera!.Total.Should().Be(5, "el total refleja las coincidencias, no la página");
        segunda!.Total.Should().Be(5);

        primera.Items.Should().HaveCount(2);
        segunda.Items.Should().HaveCount(2);

        // Páginas disjuntas: la paginación se aplica sobre el resultado ya filtrado.
        primera.Items.Select(u => u.Id).Should().NotIntersectWith(segunda.Items.Select(u => u.Id));
    }

    /// <summary>Proyección mínima de la página, para no depender del DTO completo.</summary>
    private sealed record PaginaDto(List<ItemDto> Items, int Total, int Pagina);

    private sealed record ItemDto(Guid Id, string Correo);
}
