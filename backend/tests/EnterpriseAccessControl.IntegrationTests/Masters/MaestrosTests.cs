using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Masters;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.Masters;

/// <summary>
/// CRUD y transición de estado de los cinco catálogos maestros contra SQL Server real
/// (Historia 3, RF-030 a RF-032).
/// </summary>
/// <remarks>
/// Las pruebas se parametrizan por ruta de catálogo: los cinco comparten servicio genérico, y
/// recorrerlos todos con el mismo caso garantiza que la generalización se sostiene en los cinco y no
/// solo en el que se probó.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class MaestrosTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    /// <summary>Catálogos y la longitud máxima de nombre que declara data-model.md para cada uno.</summary>
    public static TheoryData<string, int> Catalogos => new()
    {
        { "tipos-documento", 100 },
        { "tipos-sangre", 10 },
        { "generos", 50 },
        { "tipos-persona", 100 },
        { "tipos-credencial", 100 },
    };

    private static string NombreUnico(string prefijo, int limite)
    {
        var sufijo = Guid.CreateVersion7().ToString("N")[..6];
        var nombre = $"{prefijo}{sufijo}";

        // Tipo de sangre admite solo 10 caracteres: el nombre de prueba debe caber en el catálogo
        // más estrecho sin dejar de ser único.
        return nombre.Length <= limite ? nombre : nombre[..limite];
    }

    private async Task<HttpClient> ClienteAsync()
    {
        // Los catálogos son globales, pero la política CompaniaScope exige alcance administrativo.
        var compania = await fixture.Api.SembrarCompaniaAsync($"Maestros {Guid.CreateVersion7():N}"[..30]);

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"maestros.{Guid.CreateVersion7():N}@empresa.cl",
            Password,
            alcanceCompanias: [compania.Id]);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    private static Uri Coleccion(string catalogo) =>
        new($"/api/maestros/{catalogo}", UriKind.Relative);

    private static Uri Elemento(string catalogo, Guid id) =>
        new($"/api/maestros/{catalogo}/{id}", UriKind.Relative);

    private static async Task<MasterItem> CrearAsync(
        HttpClient cliente,
        string catalogo,
        string nombre,
        Estado estado = Estado.ACTIVO)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            Coleccion(catalogo), new MasterItemRequest(nombre, estado), ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<MasterItem>(ApiFactory.Json))!;
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Crear_un_valor_lo_deja_disponible_en_el_listado(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();
        var nombre = NombreUnico("Nu", limite);

        var creado = await CrearAsync(cliente, catalogo, nombre);

        creado.Nombre.Should().Be(nombre);
        creado.Estado.Should().Be(Estado.ACTIVO);
        creado.Id.Should().NotBeEmpty();

        var lista = await cliente.GetFromJsonAsync<IReadOnlyList<MasterItem>>(
            Coleccion(catalogo), ApiFactory.Json);

        lista!.Select(i => i.Id).Should().Contain(creado.Id);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Un_valor_INACTIVO_sigue_siendo_legible(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();

        var creado = await CrearAsync(cliente, catalogo, NombreUnico("In", limite));

        using var actualizacion = await cliente.PutAsJsonAsync(
            Elemento(catalogo, creado.Id),
            new MasterItemRequest(creado.Nombre, Estado.INACTIVO),
            ApiFactory.Json);

        actualizacion.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizado = await actualizacion.Content.ReadFromJsonAsync<MasterItem>(ApiFactory.Json);
        actualizado!.Estado.Should().Be(Estado.INACTIVO);

        // RF-032 aplica hacia adelante: INACTIVO retira el valor de las asignaciones nuevas, pero no
        // lo borra ni lo oculta — el histórico que lo referencia debe seguir siendo interpretable.
        var todos = await cliente.GetFromJsonAsync<IReadOnlyList<MasterItem>>(
            Coleccion(catalogo), ApiFactory.Json);

        todos!.Select(i => i.Id).Should().Contain(creado.Id);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task El_filtro_por_estado_separa_activos_de_inactivos(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();

        var activo = await CrearAsync(cliente, catalogo, NombreUnico("Ac", limite));
        var inactivo = await CrearAsync(
            cliente, catalogo, NombreUnico("Ix", limite), Estado.INACTIVO);

        var activos = await cliente.GetFromJsonAsync<IReadOnlyList<MasterItem>>(
            new Uri($"/api/maestros/{catalogo}?estado=ACTIVO", UriKind.Relative), ApiFactory.Json);

        activos.Should().NotBeNull();
        activos!.Select(i => i.Id).Should().Contain(activo.Id);
        activos.Select(i => i.Id).Should().NotContain(inactivo.Id);

        var inactivos = await cliente.GetFromJsonAsync<IReadOnlyList<MasterItem>>(
            new Uri($"/api/maestros/{catalogo}?estado=INACTIVO", UriKind.Relative), ApiFactory.Json);

        inactivos.Should().NotBeNull();
        inactivos!.Select(i => i.Id).Should().Contain(inactivo.Id);
        inactivos.Select(i => i.Id).Should().NotContain(activo.Id);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Renombrar_un_valor_persiste_el_cambio(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();

        var creado = await CrearAsync(cliente, catalogo, NombreUnico("An", limite));
        var nuevoNombre = NombreUnico("Re", limite);

        using var respuesta = await cliente.PutAsJsonAsync(
            Elemento(catalogo, creado.Id),
            new MasterItemRequest(nuevoNombre, Estado.ACTIVO),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<MasterItem>(ApiFactory.Json);
        actualizado!.Nombre.Should().Be(nuevoNombre);
        actualizado.Id.Should().Be(creado.Id, "renombrar no crea un valor nuevo");
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Actualizar_un_valor_inexistente_devuelve_404(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();

        using var respuesta = await cliente.PutAsJsonAsync(
            Elemento(catalogo, Guid.CreateVersion7()),
            new MasterItemRequest(NombreUnico("Nx", limite), Estado.ACTIVO),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Un_nombre_vacio_se_rechaza(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();
        limite.Should().BePositive();

        using var respuesta = await cliente.PostAsJsonAsync(
            Coleccion(catalogo), new MasterItemRequest("   ", Estado.ACTIVO), ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Un_nombre_mas_largo_que_la_columna_se_rechaza_con_400_y_no_revienta(
        string catalogo,
        int limite)
    {
        using var cliente = await ClienteAsync();

        // El contrato declara un máximo genérico de 200, pero cada catálogo tiene su propia columna
        // (10 para tipo de sangre). Sin validación por catálogo, esto llegaría a la base de datos y
        // saldría como error de truncamiento — un 500 en vez de un 400 explicativo.
        var demasiadoLargo = new string('X', limite + 1);

        using var respuesta = await cliente.PostAsJsonAsync(
            Coleccion(catalogo), new MasterItemRequest(demasiadoLargo, Estado.ACTIVO), ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("tipos-documento", 100)]
    [InlineData("tipos-sangre", 10)]
    [InlineData("generos", 50)]
    [InlineData("tipos-persona", 100)]
    public async Task No_se_admiten_dos_valores_con_el_mismo_nombre(string catalogo, int limite)
    {
        // Cuatro de los cinco catálogos declaran el nombre único en data-model.md; TipoCredencial
        // no lo hace y por eso queda fuera de esta prueba (hallazgo reportado).
        using var cliente = await ClienteAsync();
        var nombre = NombreUnico("Du", limite);

        await CrearAsync(cliente, catalogo, nombre);

        using var duplicado = await cliente.PostAsJsonAsync(
            Coleccion(catalogo), new MasterItemRequest(nombre, Estado.ACTIVO), ApiFactory.Json);

        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task El_nombre_se_normaliza_recortando_espacios(string catalogo, int limite)
    {
        using var cliente = await ClienteAsync();
        var nombre = NombreUnico("Es", limite - 2);

        var creado = await CrearAsync(cliente, catalogo, $"  {nombre}  ");

        // Sin recorte, " DNI" y "DNI" convivirían como valores distintos y el índice único no lo
        // impediría.
        creado.Nombre.Should().Be(nombre);
    }

    [Theory]
    [MemberData(nameof(Catalogos))]
    public async Task Los_catalogos_exigen_autenticacion(string catalogo, int limite)
    {
        limite.Should().BePositive();

        using var anonimo = fixture.Api.CrearCliente();
        using var respuesta = await anonimo.GetAsync(Coleccion(catalogo));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Los_catalogos_no_se_filtran_por_alcance_de_companias()
    {
        // Son globales al sistema: dos administradores con alcances disjuntos ven el mismo catálogo
        // (RF-030). Filtrarlos por compañía sería un error de modelo.
        using var unCliente = await ClienteAsync();
        using var otroCliente = await ClienteAsync();

        var creado = await CrearAsync(unCliente, "tipos-persona", NombreUnico("Gl", 100));

        var vistoPorElOtro = await otroCliente.GetFromJsonAsync<IReadOnlyList<MasterItem>>(
            Coleccion("tipos-persona"), ApiFactory.Json);

        vistoPorElOtro!.Select(i => i.Id).Should().Contain(creado.Id);
    }
}
