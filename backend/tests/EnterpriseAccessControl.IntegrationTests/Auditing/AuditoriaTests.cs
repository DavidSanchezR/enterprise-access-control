using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using EnterpriseAccessControl.IntegrationTests.Permissions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Auditing;

/// <summary>
/// Estampado automático de auditoría a través de los controladores reales
/// (Historia 10; RF-026, RF-027, CS-005; Principio III).
/// </summary>
/// <remarks>
/// Cada entidad la crea un usuario A y la modifica un usuario B, y todo cuerpo enviado incluye además
/// valores de auditoría falsificados. Así se verifican a la vez las tres garantías de CS-005: los
/// campos se pueblan sin intervención manual, identifican a quien realmente escribió, y el payload no
/// puede fijarlos ni reescribir la autoría de la creación.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AuditoriaTests(SqlServerFixture fixture)
{
    /// <summary>Valores que un cliente malicioso intentaría imponer.</summary>
    private static readonly DateTime FechaFalsa = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Guid UsuarioFalso = Guid.Parse("0199b0d0-dead-7000-8000-00000000beef");

    private sealed record Autores(Guid IdA, HttpClient A, Guid IdB, HttpClient B) : IDisposable
    {
        public void Dispose()
        {
            A.Dispose();
            B.Dispose();
        }
    }

    [Fact]
    public async Task Persona_se_estampa_al_crear_y_al_actualizar_ignorando_el_payload()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Auditoría personas {Sufijo()}");
        using var autores = await AutoresAsync(compania.Id);

        var inicio = DateTime.UtcNow;

        using var alta = await autores.A.PostAsJsonAsync(
            new Uri("/api/personas", UriKind.Relative),
            ConAuditoriaFalsa(PeticionPersona("Ana")),
            ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var persona = (await alta.Content.ReadFromJsonAsync<PersonaDto>(ApiFactory.Json))!;

        var creada = await LeerAsync<Persona>(persona.Id);
        VerificarAlta(creada, autores.IdA, inicio);

        // Default-deny (RF-035): editar una persona exige que pertenezca hoy a una compañía del alcance.
        // La pertenencia no toca la fila de la persona, así que no altera lo que se verifica.
        using (var pertenencia = await autores.A.PostAsJsonAsync(
                   new Uri($"/api/personas/{persona.Id}/historial-companias", UriKind.Relative),
                   new AsignacionCompaniaRequest(compania.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddYears(1)),
                   ApiFactory.Json))
        {
            pertenencia.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        (await LeerAsync<Persona>(persona.Id)).UpdatedAt.Should().Be(creada.UpdatedAt);

        using var cambio = await autores.B.PutAsJsonAsync(
            new Uri($"/api/personas/{persona.Id}", UriKind.Relative),
            ConAuditoriaFalsa(PeticionPersona("Ana María", persona.NumeroDocumento)),
            ApiFactory.Json);

        cambio.StatusCode.Should().Be(HttpStatusCode.OK);

        VerificarActualizacion(await LeerAsync<Persona>(persona.Id), creada, autores.IdB);
    }

    [Fact]
    public async Task Compania_se_estampa_al_crear_y_al_actualizar_ignorando_el_payload()
    {
        var ancla = await fixture.Api.SembrarCompaniaAsync($"Auditoría ancla {Sufijo()}");
        using var autores = await AutoresAsync(ancla.Id);

        var inicio = DateTime.UtcNow;
        var peticion = new CompaniaRequest(
            $"Auditada {Sufijo()}", ApiFactory.Maestros.Dni, Sufijo(), TipoCompania.CONTRATISTA, Estado.ACTIVO);

        using var alta = await autores.A.PostAsJsonAsync(
            new Uri("/api/companias", UriKind.Relative), ConAuditoriaFalsa(peticion), ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var compania = (await alta.Content.ReadFromJsonAsync<CompaniaDto>(ApiFactory.Json))!;

        // La compañía nueva debe entrar en el alcance de B para que pueda modificarla.
        await AmpliarAlcanceAsync(autores.IdB, compania.Id);
        using var b = await fixture.Api.CrearClienteAutenticadoAsync(autores.IdB, await CorreoDeAsync(autores.IdB));

        var creada = await LeerAsync<Compania>(compania.Id);
        VerificarAlta(creada, autores.IdA, inicio);

        using var cambio = await b.PutAsJsonAsync(
            new Uri($"/api/companias/{compania.Id}", UriKind.Relative),
            ConAuditoriaFalsa(peticion with { Nombre = $"{peticion.Nombre} renombrada" }),
            ApiFactory.Json);

        cambio.StatusCode.Should().Be(HttpStatusCode.OK);

        VerificarActualizacion(await LeerAsync<Compania>(compania.Id), creada, autores.IdB);
    }

    [Fact]
    public async Task Relacion_contratista_principal_se_estampa_al_crear_y_al_finalizar()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            $"Auditoría contratista {Sufijo()}", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync($"Auditoría principal {Sufijo()}");

        using var autores = await AutoresAsync(contratista.Id, principal.Id);

        var inicio = DateTime.UtcNow;

        using var alta = await autores.A.PostAsJsonAsync(
            new Uri($"/api/companias/{contratista.Id}/relaciones-principales", UriKind.Relative),
            ConAuditoriaFalsa(new RelacionContratistaPrincipalRequest(principal.Id, DateTime.UtcNow)),
            ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var relacion = (await alta.Content.ReadFromJsonAsync<RelacionContratistaPrincipalDto>(ApiFactory.Json))!;

        var creada = await LeerAsync<RelacionContratistaPrincipal>(relacion.Id);
        VerificarAlta(creada, autores.IdA, inicio);

        using var fin = await autores.B.PostAsync(
            new Uri($"/api/companias/{contratista.Id}/relaciones-principales/{relacion.Id}/finalizar", UriKind.Relative),
            content: null);

        fin.StatusCode.Should().Be(HttpStatusCode.NoContent);

        VerificarActualizacion(await LeerAsync<RelacionContratistaPrincipal>(relacion.Id), creada, autores.IdB);
    }

    [Fact]
    public async Task Contexto_operativo_se_estampa_al_abrirse_y_al_cerrarse_por_reemplazo()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        using var autores = await AutoresAsync(escenario.Contratista.Id, escenario.PrincipalA.Id);

        var inicio = DateTime.UtcNow;

        using var alta = await autores.A.PostAsJsonAsync(
            escenario.Ruta("contextos-operativos"),
            ConAuditoriaFalsa(new ContextoOperativoRequest(
                escenario.PrincipalA.Id, EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia)),
            ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var contexto = (await alta.Content.ReadFromJsonAsync<ContextoOperativoDto>(ApiFactory.Json))!;

        var creado = await LeerAsync<ContextoOperativoPersonaPrincipal>(contexto.Id);
        VerificarAlta(creado, autores.IdA, inicio);

        // Abrir otro contexto con la misma Principal cierra el anterior por reemplazo: esa escritura
        // sobre el contexto previo la registra quien abrió el nuevo.
        using var reemplazo = await autores.B.PostAsJsonAsync(
            escenario.Ruta("contextos-operativos"),
            ConAuditoriaFalsa(new ContextoOperativoRequest(
                escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(10), EscenarioUs5.FinPertenencia)),
            ApiFactory.Json);

        reemplazo.StatusCode.Should().Be(HttpStatusCode.Created);

        var cerrado = await LeerAsync<ContextoOperativoPersonaPrincipal>(contexto.Id);
        cerrado.MotivoFin.Should().Be(MotivoFinRevocacion.REEMPLAZO_ASIGNACION, "premisa: hubo escritura de cierre");

        VerificarActualizacion(cerrado, creado, autores.IdB);
    }

    [Fact]
    public async Task Credencial_se_estampa_al_asignarse_y_al_devolverse()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        using var autores = await AutoresAsync(escenario.Contratista.Id, escenario.PrincipalA.Id);

        var inicio = DateTime.UtcNow;

        using var alta = await autores.A.PostAsJsonAsync(
            escenario.Ruta("credenciales"),
            ConAuditoriaFalsa(new AsignacionCredencialRequest(
                escenario.PrincipalA.Id, escenario.TipoCredencialId,
                EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia)),
            ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var credencial = (await alta.Content.ReadFromJsonAsync<AsignacionCredencialDto>(ApiFactory.Json))!;

        var creada = await LeerAsync<AsignacionCredencial>(credencial.Id);
        VerificarAlta(creada, autores.IdA, inicio);

        using var devuelta = await autores.B.PostAsync(
            escenario.Ruta($"credenciales/{credencial.Id}/devolver"), content: null);

        devuelta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        VerificarActualizacion(await LeerAsync<AsignacionCredencial>(credencial.Id), creada, autores.IdB);
    }

    [Fact]
    public async Task Permiso_de_acceso_se_estampa_al_crear_y_al_actualizar_ignorando_el_payload()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();

        using var autores = await AutoresAsync(
            escenario.PrincipalA.Id, escenario.PrincipalB.Id, escenario.Contratista.Id);

        var inicio = DateTime.UtcNow;

        using var alta = await autores.A.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative),
            ConAuditoriaFalsa(escenario.Peticion(AlcancePermiso.PERSONA)),
            ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var permiso = (await alta.Content.ReadFromJsonAsync<PermisoAccesoDto>(ApiFactory.Json))!;

        var creado = await LeerAsync<PermisoAcceso>(permiso.Id);
        VerificarAlta(creado, autores.IdA, inicio);

        using var cambio = await autores.B.PutAsJsonAsync(
            new Uri($"/api/permisos/{permiso.Id}", UriKind.Relative),
            ConAuditoriaFalsa(escenario.Peticion(AlcancePermiso.PERSONA, estado: Estado.INACTIVO)),
            ApiFactory.Json);

        cambio.StatusCode.Should().Be(HttpStatusCode.OK);

        VerificarActualizacion(await LeerAsync<PermisoAcceso>(permiso.Id), creado, autores.IdB);
    }

    [Fact]
    public async Task Los_bloques_horarios_de_un_permiso_tambien_se_estampan()
    {
        // Entidad hija sin controlador propio: se escribe dentro del caso de uso del permiso y el
        // interceptor la alcanza igual.
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        using var autores = await AutoresAsync(escenario.PrincipalA.Id, escenario.Contratista.Id);

        var inicio = DateTime.UtcNow;

        using var alta = await autores.A.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative),
            escenario.Peticion(AlcancePermiso.PERSONA),
            ApiFactory.Json);

        var permiso = (await alta.Content.ReadFromJsonAsync<PermisoAccesoDto>(ApiFactory.Json))!;

        List<BloqueHorarioPermiso> bloques = [];
        await fixture.Api.ConDbContextAsync(async db => bloques = await db.Set<BloqueHorarioPermiso>()
            .AsNoTracking().Where(b => b.PermisoAccesoId == permiso.Id).ToListAsync());

        bloques.Should().NotBeEmpty();
        bloques.ForEach(b => VerificarAlta(b, autores.IdA, inicio));
    }

    // --- Verificaciones ------------------------------------------------------------------------------

    private static void VerificarAlta(IAuditable entidad, Guid autor, DateTime inicioPrueba)
    {
        entidad.CreatedById.Should().Be(autor, "la autoría la fija el servidor, no el payload");
        entidad.UpdatedById.Should().Be(autor);

        entidad.CreatedAt.Should().NotBe(FechaFalsa);
        entidad.CreatedAt.Should().BeCloseTo(inicioPrueba, TimeSpan.FromMinutes(2));
        entidad.UpdatedAt.Should().Be(entidad.CreatedAt, "en el alta ambas marcas coinciden");

        entidad.CreatedById.Should().NotBe(UsuarioFalso);
    }

    private static void VerificarActualizacion(IAuditable despues, IAuditable antes, Guid editor)
    {
        // La autoría de la creación es inmutable (Principio III).
        despues.CreatedById.Should().Be(antes.CreatedById);
        despues.CreatedAt.Should().Be(antes.CreatedAt);

        despues.UpdatedById.Should().Be(editor).And.NotBe(UsuarioFalso);
        despues.UpdatedAt.Should().BeOnOrAfter(antes.UpdatedAt).And.NotBe(FechaFalsa);
    }

    // --- Montaje --------------------------------------------------------------------------------------

    private async Task<Autores> AutoresAsync(params Guid[] companias)
    {
        var a = await fixture.Api.SembrarUsuarioAsync(
            $"auditor.a.{Guid.CreateVersion7():N}@empresa.cl", "Contrasena1Segura", alcanceCompanias: companias);
        var b = await fixture.Api.SembrarUsuarioAsync(
            $"auditor.b.{Guid.CreateVersion7():N}@empresa.cl", "Contrasena1Segura", alcanceCompanias: companias);

        return new Autores(
            a.Id, await fixture.Api.CrearClienteAutenticadoAsync(a.Id, a.Correo),
            b.Id, await fixture.Api.CrearClienteAutenticadoAsync(b.Id, b.Correo));
    }

    private Task AmpliarAlcanceAsync(Guid usuarioId, Guid companiaId) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<AlcanceUsuarioCompania>().Add(new AlcanceUsuarioCompania
            {
                UsuarioId = usuarioId,
                CompaniaId = companiaId,
            });

            await db.SaveChangesAsync();
        });

    private async Task<string> CorreoDeAsync(Guid usuarioId)
    {
        var correo = string.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
            correo = await db.Set<Usuario>().Where(u => u.Id == usuarioId).Select(u => u.Correo).SingleAsync());

        return correo;
    }

    private async Task<T> LeerAsync<T>(Guid id)
        where T : EntidadBase
    {
        T? entidad = null;

        await fixture.Api.ConDbContextAsync(async db =>
            entidad = await db.Set<T>().AsNoTracking().SingleAsync(e => e.Id == id));

        return entidad!;
    }

    /// <summary>Serializa la petición y le añade campos de auditoría falsificados.</summary>
    private static JsonObject ConAuditoriaFalsa(object peticion)
    {
        var cuerpo = JsonSerializer.SerializeToNode(peticion, ApiFactory.Json)!.AsObject();

        cuerpo["createdAt"] = FechaFalsa;
        cuerpo["updatedAt"] = FechaFalsa;
        cuerpo["createdById"] = UsuarioFalso;
        cuerpo["updatedById"] = UsuarioFalso;

        return cuerpo;
    }

    private static PersonaRequest PeticionPersona(string nombres, string? numero = null) =>
        new(
            nombres,
            "Auditada",
            new DateOnly(1990, 5, 20),
            ApiFactory.Maestros.Dni,
            numero ?? Sufijo()[..8],
            ApiFactory.Maestros.Masculino,
            $"{Guid.CreateVersion7():N}@empresa.cl",
            ApiFactory.Maestros.OPositivo,
            "Contacto de auditoría",
            "+51 999 999 999");

    private static string Sufijo() => Guid.CreateVersion7().ToString("N")[..12];
}
