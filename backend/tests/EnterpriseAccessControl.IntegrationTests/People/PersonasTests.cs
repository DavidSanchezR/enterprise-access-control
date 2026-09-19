using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Registro de personas y unicidad global del documento (Historia 4, RF-012, RF-013, RF-041).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PersonasTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string DocumentoUnico() => Guid.CreateVersion7().ToString("N")[..12];

    private static PersonaRequest Peticion(
        string? numeroDocumento = null,
        Guid? tipoDocumentoId = null,
        string nombres = "Ana",
        string apellidos = "Pérez",
        string correo = "ana.perez@empresa.cl") =>
        new(
            nombres,
            apellidos,
            new DateOnly(1990, 5, 20),
            tipoDocumentoId ?? ApiFactory.Maestros.Dni,
            numeroDocumento ?? DocumentoUnico(),
            ApiFactory.Maestros.Masculino,
            correo,
            ApiFactory.Maestros.OPositivo,
            "María Pérez",
            "+51 999 111 222");

    private async Task<(HttpClient Cliente, Compania Compania)> ClienteAsync()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Personas {DocumentoUnico()}");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"personas.{Guid.CreateVersion7():N}@empresa.cl",
            Password,
            alcanceCompanias: [compania.Id]);

        return (await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo), compania);
    }

    [Fact]
    public async Task Registrar_una_persona_con_todos_los_campos_obligatorios()
    {
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative), Peticion(), ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creada = await respuesta.Content.ReadFromJsonAsync<PersonaDto>(ApiFactory.Json);

        creada!.Id.Should().NotBeEmpty("el identificador lo genera el servidor (RF-013)");
        creada.Nombres.Should().Be("Ana");
        creada.Apellidos.Should().Be("Pérez");
        creada.FechaNacimiento.Should().Be(new DateOnly(1990, 5, 20));
        creada.ContactoEmergencia.Should().Be("María Pérez");
    }

    [Fact]
    public async Task No_se_admiten_dos_personas_con_el_mismo_documento()
    {
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        var numero = DocumentoUnico();

        using (var primera = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative), Peticion(numero), ApiFactory.Json))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var duplicada = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative),
            Peticion(numero, nombres: "Otro", apellidos: "Homónimo"),
            ApiFactory.Json);

        // RF-041: el mismo documento es la misma persona; duplicarla partiría su histórico de acceso.
        duplicada.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await duplicada.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("DOCUMENTO_YA_REGISTRADO");
    }

    [Fact]
    public async Task El_mismo_numero_con_otro_tipo_de_documento_si_se_admite()
    {
        // La unicidad es del par, no del número suelto: un DNI y un pasaporte pueden coincidir en
        // dígitos sin ser la misma persona.
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        var numero = DocumentoUnico();

        using (var conDni = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative), Peticion(numero), ApiFactory.Json))
        {
            conDni.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var conPasaporte = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative),
            Peticion(numero, ApiFactory.Maestros.Pasaporte),
            ApiFactory.Json);

        conPasaporte.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task La_unicidad_del_documento_la_respalda_la_base_de_datos()
    {
        // No basta con la comprobación previa de la aplicación: dos altas concurrentes podrían
        // superarla ambas. El índice único es la garantía real.
        var numero = DocumentoUnico();

        await fixture.Api.SembrarPersonaAsync(numero);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<Persona>().Add(new Persona
            {
                Nombres = "Duplicada",
                Apellidos = "Concurrente",
                FechaNacimiento = new DateOnly(1985, 1, 1),
                TipoDocumentoId = ApiFactory.Maestros.Dni,
                NumeroDocumento = numero,
                GeneroId = ApiFactory.Maestros.Masculino,
                CorreoElectronico = "dup@empresa.cl",
                TipoSangreId = ApiFactory.Maestros.OPositivo,
                ContactoEmergencia = "X",
                NumeroEmergencia = "1",
            });

            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().ThrowAsync<DbUpdateException>();
        });
    }

    [Fact]
    public async Task Los_campos_obligatorios_ausentes_se_rechazan_con_400()
    {
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative),
            Peticion() with { Nombres = "", ContactoEmergencia = "" },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Un_correo_con_formato_invalido_se_rechaza()
    {
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative),
            Peticion(correo: "no-es-un-correo"),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_valor_de_catalogo_inexistente_se_rechaza_con_400()
    {
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative),
            Peticion(tipoDocumentoId: Guid.CreateVersion7()),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("VALOR_MAESTRO_INACTIVO");
    }

    [Fact]
    public async Task Una_fecha_de_nacimiento_futura_se_rechaza()
    {
        var (cliente, _) = await ClienteAsync();
        using var _c = cliente;

        var futura = Peticion() with
        {
            FechaNacimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
        };

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative), futura, ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Actualizar_una_persona_dentro_del_alcance_persiste_los_cambios()
    {
        var (cliente, compania) = await ClienteAsync();
        using var _c = cliente;

        var persona = await fixture.Api.SembrarPersonaAsync(companiaId: compania.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/personas/{persona.Id}", UriKind.Relative),
            Peticion(persona.NumeroDocumento, nombres: "Ana María", apellidos: "Pérez Soto"),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizada = await respuesta.Content.ReadFromJsonAsync<PersonaDto>(ApiFactory.Json);
        actualizada!.Nombres.Should().Be("Ana María");
        actualizada.Apellidos.Should().Be("Pérez Soto");
        actualizada.Id.Should().Be(persona.Id, "el identificador es inmutable (RF-013)");
    }

    [Fact]
    public async Task Actualizar_no_puede_apropiarse_del_documento_de_otra_persona()
    {
        var (cliente, compania) = await ClienteAsync();
        using var _c = cliente;

        var ocupado = await fixture.Api.SembrarPersonaAsync(companiaId: compania.Id);
        var aEditar = await fixture.Api.SembrarPersonaAsync(companiaId: compania.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/personas/{aEditar.Id}", UriKind.Relative),
            Peticion(ocupado.NumeroDocumento),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Conservar_el_propio_documento_al_actualizar_no_se_considera_duplicado()
    {
        var (cliente, compania) = await ClienteAsync();
        using var _c = cliente;

        var persona = await fixture.Api.SembrarPersonaAsync(companiaId: compania.Id);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/personas/{persona.Id}", UriKind.Relative),
            Peticion(persona.NumeroDocumento, nombres: "Solo cambia el nombre"),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Las_operaciones_de_persona_exigen_autenticacion()
    {
        using var anonimo = fixture.Api.CrearCliente();

        using var respuesta = await anonimo.GetAsync(new Uri("/api/personas", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
