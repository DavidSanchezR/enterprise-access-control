using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.Companies;

/// <summary>
/// Alta y clasificación de compañías, filtrado por tipo y filtrado por alcance del usuario
/// (Historia 2, RF-006, RF-042; Historia 1 criterio 4 revalidado).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class CompaniasTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private static CompaniaRequest Peticion(
        string nombre,
        TipoCompania tipo = TipoCompania.PRINCIPAL_MANDANTE,
        Estado estado = Estado.ACTIVO,
        Guid? tipoDocumentoId = null,
        string? numeroDocumento = null) =>
        new(
            nombre,
            tipoDocumentoId ?? Guid.CreateVersion7(),
            numeroDocumento ?? Guid.CreateVersion7().ToString("N")[..12],
            tipo,
            estado);

    /// <summary>Crea un administrador cuyo alcance cubre exactamente las compañías indicadas.</summary>
    private async Task<HttpClient> ClienteConAlcanceAsync(params Guid[] companiaIds)
    {
        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"),
            Password,
            alcanceCompanias: companiaIds);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    [Fact]
    public async Task Crear_una_compania_la_clasifica_como_principal_o_contratista()
    {
        var existente = await fixture.Api.SembrarCompaniaAsync("Ancla");
        using var cliente = await ClienteConAlcanceAsync(existente.Id);

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/companias", UriKind.Relative),
            Peticion("Minera Los Andes", TipoCompania.PRINCIPAL_MANDANTE),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creada = await respuesta.Content.ReadFromJsonAsync<CompaniaDto>(ApiFactory.Json);
        creada!.Nombre.Should().Be("Minera Los Andes");
        creada.TipoCompania.Should().Be(TipoCompania.PRINCIPAL_MANDANTE);
        creada.Estado.Should().Be(Estado.ACTIVO);
        creada.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task No_se_admiten_dos_companias_con_el_mismo_documento()
    {
        var ancla = await fixture.Api.SembrarCompaniaAsync("Ancla");
        using var cliente = await ClienteConAlcanceAsync(ancla.Id);

        var tipoDocumento = Guid.CreateVersion7();
        var numero = Guid.CreateVersion7().ToString("N")[..12];

        using var primera = await cliente.PostAsJsonAsync(
            new Uri("/api/companias", UriKind.Relative),
            Peticion("Primera", tipoDocumentoId: tipoDocumento, numeroDocumento: numero),
            ApiFactory.Json);

        primera.StatusCode.Should().Be(HttpStatusCode.Created);

        using var duplicada = await cliente.PostAsJsonAsync(
            new Uri("/api/companias", UriKind.Relative),
            Peticion("Segunda", tipoDocumentoId: tipoDocumento, numeroDocumento: numero),
            ApiFactory.Json);

        // Una compañía se identifica por su documento legal, no por su nombre.
        duplicada.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task El_listado_filtra_por_tipo_de_compania()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("P", TipoCompania.PRINCIPAL_MANDANTE);
        var contratista = await fixture.Api.SembrarCompaniaAsync("C", TipoCompania.CONTRATISTA);

        using var cliente = await ClienteConAlcanceAsync(principal.Id, contratista.Id);

        var todas = await cliente.GetFromJsonAsync<PaginaResponse<CompaniaDto>>(
            new Uri("/api/companias", UriKind.Relative), ApiFactory.Json);

        todas!.Items.Should().HaveCount(2);

        var soloPrincipales = await cliente.GetFromJsonAsync<PaginaResponse<CompaniaDto>>(
            new Uri("/api/companias?tipoCompania=PRINCIPAL_MANDANTE", UriKind.Relative),
            ApiFactory.Json);

        soloPrincipales!.Items.Should().ContainSingle()
            .Which.Id.Should().Be(principal.Id);
    }

    [Fact]
    public async Task El_listado_solo_devuelve_companias_dentro_del_alcance_del_usuario()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync("Dentro del alcance");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Fuera del alcance");

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        var pagina = await cliente.GetFromJsonAsync<PaginaResponse<CompaniaDto>>(
            new Uri("/api/companias", UriKind.Relative), ApiFactory.Json);

        pagina!.Items.Should().ContainSingle().Which.Id.Should().Be(mia.Id);
        pagina.Items.Should().NotContain(c => c.Id == ajena.Id);
    }

    [Fact]
    public async Task Consultar_por_id_una_compania_fuera_de_alcance_devuelve_404_y_no_403()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/companias/{ajena.Id}", UriKind.Relative));

        // 404 y no 403: un 403 confirmaría que esa compañía existe, permitiendo enumerarlas por id
        // (Principio I, RF-005).
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_se_puede_actualizar_una_compania_fuera_de_alcance()
    {
        var mia = await fixture.Api.SembrarCompaniaAsync("Propia");
        var ajena = await fixture.Api.SembrarCompaniaAsync("Ajena");

        using var cliente = await ClienteConAlcanceAsync(mia.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/companias/{ajena.Id}", UriKind.Relative),
            Peticion("Renombrada por quien no debe"),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Actualizar_una_compania_en_alcance_persiste_los_cambios()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Nombre original");
        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/companias/{compania.Id}", UriKind.Relative),
            Peticion(
                "Nombre corregido",
                TipoCompania.CONTRATISTA,
                Estado.INACTIVO,
                compania.TipoDocumentoId,
                compania.NumeroDocumento),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizada = await respuesta.Content.ReadFromJsonAsync<CompaniaDto>(ApiFactory.Json);
        actualizada!.Nombre.Should().Be("Nombre corregido");
        actualizada.TipoCompania.Should().Be(TipoCompania.CONTRATISTA);
        actualizada.Estado.Should().Be(Estado.INACTIVO);
    }

    [Fact]
    public async Task El_listado_busca_por_nombre_y_por_numero_de_documento()
    {
        var buscada = await fixture.Api.SembrarCompaniaAsync("Constructora Aurora");
        var otra = await fixture.Api.SembrarCompaniaAsync("Servicios Boreal");

        using var cliente = await ClienteConAlcanceAsync(buscada.Id, otra.Id);

        var porNombre = await cliente.GetFromJsonAsync<PaginaResponse<CompaniaDto>>(
            new Uri("/api/companias?texto=Aurora", UriKind.Relative), ApiFactory.Json);

        porNombre!.Items.Should().ContainSingle().Which.Id.Should().Be(buscada.Id);

        var porDocumento = await cliente.GetFromJsonAsync<PaginaResponse<CompaniaDto>>(
            new Uri($"/api/companias?texto={buscada.NumeroDocumento}", UriKind.Relative),
            ApiFactory.Json);

        porDocumento!.Items.Should().ContainSingle().Which.Id.Should().Be(buscada.Id);
    }

    [Fact]
    public async Task El_tipo_de_compania_viaja_como_literal_de_texto()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Literal", TipoCompania.CONTRATISTA);
        using var cliente = await ClienteConAlcanceAsync(compania.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/companias/{compania.Id}", UriKind.Relative));

        var json = await respuesta.Content.ReadAsStringAsync();

        // El literal persistido en nvarchar y el emitido en JSON deben coincidir exactamente: así
        // no hay capa de traducción que pueda desincronizarse (research.md §17).
        json.Should().Contain("\"CONTRATISTA\"");
    }
}
