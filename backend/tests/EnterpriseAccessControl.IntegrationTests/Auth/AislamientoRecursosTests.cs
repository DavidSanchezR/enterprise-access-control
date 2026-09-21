using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.AreaAccess;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

// Los dos soportes definen `CrearRaizAsync` como extensión de HttpClient: se llaman por su clase para
// que la resolución no sea ambigua.
using AreasSoporte = EnterpriseAccessControl.IntegrationTests.AreaAccess.AreasAccesoSoporte;
using UnidadesSoporte = EnterpriseAccessControl.IntegrationTests.OrgUnits.UnidadesOrganizativasSoporte;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// CS-037 — aislamiento por alcance en los **siete** tipos de recurso que el criterio declara
/// (RF-077; research.md §34.2).
/// </summary>
/// <remarks>
/// Cierra el hallazgo C1/C2 de la auditoría: CS-037 figuraba como cubierto, pero ninguna prueba lo
/// verificaba. Las suites `Aislamiento*Tests` previas comprueban el aislamiento **de datos** entre
/// Compañías Principales; estas comprueban el aislamiento **por actor autenticado**, que es lo que
/// RF-077 exige y lo que una regresión en el alcance rompería.
///
/// Cada prueba sigue el mismo principio, aunque el flujo técnico de cada recurso difiera:
/// <list type="bullet">
/// <item>el recurso se crea con datos reales y su identificador se **conoce**;</item>
/// <item>un <c>COMPANY_ADMINISTRATOR</c> de otra compañía lo pide y recibe <c>404</c>, nunca
/// <c>403</c>: distinguirlos confirmaría su existencia;</item>
/// <item>un <c>GLOBAL_ADMINISTRATOR</c> alcanza recursos de **dos** Principales distintas en la misma
/// prueba, lo que falsaría cualquier regresión que volviera a exigir un alcance enumerado.</item>
/// </list>
/// Todo pasa por HTTP contra la API real: lo que se verifica es que la autorización ocurre en el
/// backend, no en la interfaz (Principio I).
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AislamientoRecursosTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"cs037.{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private static string Sufijo() => Guid.CreateVersion7().ToString("N")[..10];

    /// <summary>Dos Principales, un admin de la primera y un administrador global.</summary>
    private async Task<Escena> MontarAsync()
    {
        var sufijo = Sufijo();

        var principalA = await fixture.Api.SembrarCompaniaAsync($"Principal A {sufijo}");
        var principalB = await fixture.Api.SembrarCompaniaAsync($"Principal B {sufijo}");

        var adminA = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin-a"), Password, alcanceCompanias: [principalA.Id]);

        var global = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        return new Escena(
            principalA,
            principalB,
            await fixture.Api.CrearClienteAutenticadoAsync(adminA.Id, adminA.Correo),
            await fixture.Api.CrearClienteAutenticadoAsync(global.Id, global.Correo));
    }

    private sealed record Escena(
        Compania PrincipalA,
        Compania PrincipalB,
        HttpClient DeLaA,
        HttpClient Global) : IDisposable
    {
        public void Dispose()
        {
            DeLaA.Dispose();
            Global.Dispose();
        }
    }

    // --- 1. Usuarios ----------------------------------------------------------------------------

    [Fact]
    public async Task CS037_usuario_de_otra_compania_responde_404_y_el_global_alcanza_ambas()
    {
        using var escena = await MontarAsync();

        var deLaA = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("usuario-a"), Password, alcanceCompanias: [escena.PrincipalA.Id]);

        var deLaB = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("usuario-b"), Password, alcanceCompanias: [escena.PrincipalB.Id]);

        // El id es real y conocido: conocerlo no otorga autorización (RF-077).
        using var ajeno = await escena.DeLaA.GetAsync(
            new Uri($"/api/usuarios/{deLaB.Id}", UriKind.Relative));

        ajeno.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propio = await escena.DeLaA.GetAsync(
            new Uri($"/api/usuarios/{deLaA.Id}", UriKind.Relative));

        propio.StatusCode.Should().Be(HttpStatusCode.OK);

        await ElGlobalAlcanzaAmbosAsync(escena.Global, $"/api/usuarios/{deLaA.Id}", $"/api/usuarios/{deLaB.Id}");
    }

    // --- 2. Compañías ---------------------------------------------------------------------------

    [Fact]
    public async Task CS037_compania_fuera_de_alcance_responde_404_y_el_global_alcanza_ambas()
    {
        using var escena = await MontarAsync();

        using var ajena = await escena.DeLaA.GetAsync(
            new Uri($"/api/companias/{escena.PrincipalB.Id}", UriKind.Relative));

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propia = await escena.DeLaA.GetAsync(
            new Uri($"/api/companias/{escena.PrincipalA.Id}", UriKind.Relative));

        propia.StatusCode.Should().Be(HttpStatusCode.OK);

        // El listado tampoco la insinúa: ni en los elementos ni en el total (UX-22).
        var pagina = await escena.DeLaA.GetFromJsonAsync<PaginaDto<CompaniaResumen>>(
            new Uri("/api/companias?tamañoPagina=200", UriKind.Relative), ApiFactory.Json);

        pagina!.Items.Should().NotContain(c => c.Id == escena.PrincipalB.Id);
        pagina.Total.Should().Be(pagina.Items.Count);

        await ElGlobalAlcanzaAmbosAsync(
            escena.Global,
            $"/api/companias/{escena.PrincipalA.Id}",
            $"/api/companias/{escena.PrincipalB.Id}");
    }

    // --- 3. Unidades Organizativas -------------------------------------------------------------

    [Fact]
    public async Task CS037_unidad_organizativa_de_otra_principal_responde_404_y_el_global_alcanza_ambas()
    {
        using var escena = await MontarAsync();

        // Las crea el administrador global, que sí alcanza a las dos Principales.
        var raizA = await UnidadesSoporte.CrearRaizAsync(
            escena.Global, $"Gerencia A {Sufijo()}", escena.PrincipalA.Id);

        var raizB = await UnidadesSoporte.CrearRaizAsync(
            escena.Global, $"Gerencia B {Sufijo()}", escena.PrincipalB.Id);

        using var ajena = await escena.DeLaA.GetAsync(
            new Uri($"/api/unidades-organizativas/{raizB.Id}", UriKind.Relative));

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propia = await escena.DeLaA.GetAsync(
            new Uri($"/api/unidades-organizativas/{raizA.Id}", UriKind.Relative));

        propia.StatusCode.Should().Be(HttpStatusCode.OK);

        // Escritura fuera de alcance: tampoco puede renombrarla conociendo su id.
        using var escritura = await escena.DeLaA.PutAsJsonAsync(
            new Uri($"/api/unidades-organizativas/{raizB.Id}", UriKind.Relative),
            new UnidadOrganizativaUpdateRequest("Renombrada sin permiso", Estado.ACTIVO),
            ApiFactory.Json);

        escritura.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ElGlobalAlcanzaAmbosAsync(
            escena.Global,
            $"/api/unidades-organizativas/{raizA.Id}",
            $"/api/unidades-organizativas/{raizB.Id}");
    }

    // --- 4. Áreas de Acceso ---------------------------------------------------------------------

    [Fact]
    public async Task CS037_area_de_acceso_de_otra_principal_responde_404_y_el_global_alcanza_ambas()
    {
        using var escena = await MontarAsync();

        var areaA = await AreasSoporte.CrearRaizAsync(
            escena.Global, $"Planta A {Sufijo()}", escena.PrincipalA.Id);

        var areaB = await AreasSoporte.CrearRaizAsync(
            escena.Global, $"Planta B {Sufijo()}", escena.PrincipalB.Id);

        using var ajena = await escena.DeLaA.GetAsync(
            new Uri($"/api/areas-acceso/{areaB.Id}", UriKind.Relative));

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propia = await escena.DeLaA.GetAsync(
            new Uri($"/api/areas-acceso/{areaA.Id}", UriKind.Relative));

        propia.StatusCode.Should().Be(HttpStatusCode.OK);

        using var escritura = await escena.DeLaA.PutAsJsonAsync(
            new Uri($"/api/areas-acceso/{areaB.Id}", UriKind.Relative),
            new AreaAccesoUpdateRequest("Renombrada sin permiso", Estado.ACTIVO),
            ApiFactory.Json);

        escritura.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ElGlobalAlcanzaAmbosAsync(
            escena.Global,
            $"/api/areas-acceso/{areaA.Id}",
            $"/api/areas-acceso/{areaB.Id}");
    }

    // --- 5. Personas ----------------------------------------------------------------------------

    [Fact]
    public async Task CS037_persona_de_otra_compania_responde_404_y_el_global_alcanza_ambas()
    {
        using var escena = await MontarAsync();

        // El alcance de `Persona` es por unión: pertenencia vigente o contexto operativo vigente
        // (RF-077). Aquí basta la pertenencia para situar cada persona en una compañía distinta.
        var personaA = await fixture.Api.SembrarPersonaAsync(companiaId: escena.PrincipalA.Id);
        var personaB = await fixture.Api.SembrarPersonaAsync(companiaId: escena.PrincipalB.Id);

        using var ajena = await escena.DeLaA.GetAsync(
            new Uri($"/api/personas/{personaB.Id}", UriKind.Relative));

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propia = await escena.DeLaA.GetAsync(
            new Uri($"/api/personas/{personaA.Id}", UriKind.Relative));

        propia.StatusCode.Should().Be(HttpStatusCode.OK);

        // El histórico de la persona ajena tampoco es alcanzable por su id.
        using var historico = await escena.DeLaA.GetAsync(
            new Uri($"/api/personas/{personaB.Id}/historial-companias", UriKind.Relative));

        historico.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ElGlobalAlcanzaAmbosAsync(
            escena.Global,
            $"/api/personas/{personaA.Id}",
            $"/api/personas/{personaB.Id}");
    }

    // --- 6. Contextos Operativos ----------------------------------------------------------------

    [Fact]
    public async Task CS037_contexto_operativo_de_otra_principal_responde_404_y_el_global_alcanza_ambas()
    {
        using var escena = await MontarAsync();

        var (personaA, _) = await ConContextoAsync(escena.Global, escena.PrincipalA.Id);
        var (personaB, contextoB) = await ConContextoAsync(escena.Global, escena.PrincipalB.Id);

        // La persona de la Principal B queda fuera del alcance del admin de A, y con ella su contexto.
        using var ajeno = await escena.DeLaA.GetAsync(
            new Uri($"/api/personas/{personaB.Id}/contextos-operativos", UriKind.Relative));

        ajeno.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propio = await escena.DeLaA.GetAsync(
            new Uri($"/api/personas/{personaA.Id}/contextos-operativos", UriKind.Relative));

        propio.StatusCode.Should().Be(HttpStatusCode.OK);

        // Escritura sobre el contexto ajeno conociendo sus dos identificadores: tampoco. Se usa la
        // asignación de unidad organizativa, que es la operación de escritura que el contrato expone
        // sobre un contexto concreto (los contextos no tienen `/finalizar` propio).
        using var escritura = await escena.DeLaA.PostAsJsonAsync(
            new Uri(
                $"/api/personas/{personaB.Id}/contextos-operativos/{contextoB.Id}/unidad-organizativa",
                UriKind.Relative),
            new AsignacionUnidadOrganizativaRequest(
                Guid.CreateVersion7(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)),
            ApiFactory.Json);

        escritura.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ElGlobalAlcanzaAmbosAsync(
            escena.Global,
            $"/api/personas/{personaA.Id}/contextos-operativos",
            $"/api/personas/{personaB.Id}/contextos-operativos");
    }

    // --- 7. Credenciales ------------------------------------------------------------------------

    [Fact]
    public async Task CS037_credencial_de_otra_principal_no_se_lista_ni_se_opera_por_su_id()
    {
        using var escena = await MontarAsync();

        var tipoCredencialId = await SembrarTipoCredencialAsync();

        var (personaA, _) = await ConContextoAsync(escena.Global, escena.PrincipalA.Id);
        var (personaB, _) = await ConContextoAsync(escena.Global, escena.PrincipalB.Id);

        var credencialA = await AsignarCredencialAsync(
            escena.Global, personaA.Id, escena.PrincipalA.Id, tipoCredencialId);

        var credencialB = await AsignarCredencialAsync(
            escena.Global, personaB.Id, escena.PrincipalB.Id, tipoCredencialId);

        // Las credenciales cuelgan de la persona (`api/personas/{id}/credenciales`), así que su
        // superficie de autorización es la de la persona: la de la Principal B queda fuera de alcance.
        using var listadoAjeno = await escena.DeLaA.GetAsync(
            new Uri($"/api/personas/{personaB.Id}/credenciales", UriKind.Relative));

        listadoAjeno.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Y las operaciones por id sobre la ajena no son alcanzables, aunque el id sea correcto.
        using var devolver = await escena.DeLaA.PostAsync(
            new Uri(
                $"/api/personas/{personaB.Id}/credenciales/{credencialB.Id}/devolver",
                UriKind.Relative),
            content: null);

        devolver.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var eliminar = await escena.DeLaA.DeleteAsync(
            new Uri(
                $"/api/personas/{personaB.Id}/credenciales/{credencialB.Id}",
                UriKind.Relative));

        eliminar.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // La propia sí es alcanzable: el 404 anterior es por alcance, no porque la ruta falle siempre.
        var listadoPropio = await escena.DeLaA.GetFromJsonAsync<List<CredencialResumen>>(
            new Uri($"/api/personas/{personaA.Id}/credenciales", UriKind.Relative), ApiFactory.Json);

        listadoPropio!.Select(c => c.Id).Should().Contain(credencialA.Id);

        // El administrador global sí opera sobre las dos Principales.
        var globalEnA = await escena.Global.GetFromJsonAsync<List<CredencialResumen>>(
            new Uri($"/api/personas/{personaA.Id}/credenciales", UriKind.Relative), ApiFactory.Json);

        var globalEnB = await escena.Global.GetFromJsonAsync<List<CredencialResumen>>(
            new Uri($"/api/personas/{personaB.Id}/credenciales", UriKind.Relative), ApiFactory.Json);

        globalEnA!.Select(c => c.Id).Should().Contain(credencialA.Id);
        globalEnB!.Select(c => c.Id).Should().Contain(credencialB.Id);
    }

    // --- Servicios sin recurso propio: estado efectivo y revocación en cascada -------------------

    /// <remarks>
    /// <c>EstadoEfectivoService</c> y <c>RevocacionService</c> no exponen un recurso propio: el primero
    /// es una proyección de la persona y el segundo un colaborador interno cuyo único invocador es
    /// <c>HistorialPersonaService</c>. Su superficie de autorización es por tanto la de la persona, y es
    /// por ahí por donde hay que ejercitarla — no inventando un endpoint que no existe.
    /// </remarks>
    [Fact]
    public async Task CS037_el_estado_efectivo_de_una_persona_ajena_responde_404()
    {
        using var escena = await MontarAsync();

        var personaA = await fixture.Api.SembrarPersonaAsync(companiaId: escena.PrincipalA.Id);
        var personaB = await fixture.Api.SembrarPersonaAsync(companiaId: escena.PrincipalB.Id);

        var instante = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));

        using var ajena = await escena.DeLaA.GetAsync(new Uri(
            $"/api/personas/{personaB.Id}/estado-efectivo?fechaHora={instante}", UriKind.Relative));

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var propia = await escena.DeLaA.GetAsync(new Uri(
            $"/api/personas/{personaA.Id}/estado-efectivo?fechaHora={instante}", UriKind.Relative));

        propia.StatusCode.Should().Be(HttpStatusCode.OK);

        await ElGlobalAlcanzaAmbosAsync(
            escena.Global,
            $"/api/personas/{personaA.Id}/estado-efectivo?fechaHora={instante}",
            $"/api/personas/{personaB.Id}/estado-efectivo?fechaHora={instante}");
    }

    [Fact]
    public async Task CS037_no_se_dispara_la_cascada_de_revocacion_sobre_una_persona_ajena()
    {
        using var escena = await MontarAsync();

        var ahora = DateTime.UtcNow;

        var personaB = await fixture.Api.SembrarPersonaAsync(
            companiaId: escena.PrincipalB.Id,
            inicioVigencia: ahora.AddMonths(-6),
            finVigencia: ahora.AddYears(2));

        // Se conoce el id de la pertenencia ajena y es válido: finalizarla dispararía la cascada de
        // revocación (RF-061) sobre contextos, unidades y credenciales de una persona fuera de alcance.
        var pertenenciaAjenaId = Guid.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
            pertenenciaAjenaId = await db.Set<AsignacionPersonaCompania>()
                .Where(a => a.PersonaId == personaB.Id)
                .Select(a => a.Id)
                .SingleAsync());

        using var respuesta = await escena.DeLaA.PostAsJsonAsync(
            new Uri(
                $"/api/personas/{personaB.Id}/historial-companias/{pertenenciaAjenaId}/finalizar",
                UriKind.Relative),
            new FinalizarPertenenciaRequest(ahora),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Y la pertenencia sigue intacta: el 404 no es cosmético, no hubo escritura.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var pertenencia = await db.Set<AsignacionPersonaCompania>()
                .AsNoTracking()
                .SingleAsync(a => a.Id == pertenenciaAjenaId);

            pertenencia.FechaHoraFin.Should().BeAfter(ahora.AddYears(1));
            pertenencia.MotivoFin.Should().BeNull();
        });
    }

    // --- Apoyo ----------------------------------------------------------------------------------

    /// <summary>
    /// El administrador global alcanza dos recursos de Principales distintas en la misma prueba.
    /// </summary>
    /// <remarks>
    /// Es la mitad que falsaría una regresión que volviera a exigir <c>CompaniaIds.Count &gt; 0</c>:
    /// un alcance GLOBAL que tuviera que enumerar compañías fallaría aquí, no en el caso negativo.
    /// </remarks>
    private static async Task ElGlobalAlcanzaAmbosAsync(
        HttpClient global,
        string rutaA,
        string rutaB)
    {
        using var enA = await global.GetAsync(new Uri(rutaA, UriKind.Relative));
        using var enB = await global.GetAsync(new Uri(rutaB, UriKind.Relative));

        enA.StatusCode.Should().Be(HttpStatusCode.OK);
        enB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Persona con pertenencia y contexto operativo vigentes en la Principal indicada.</summary>
    /// <remarks>
    /// La pertenencia se siembra con una ventana deliberadamente más amplia que la del contexto: RF-072
    /// exige que el contexto quede **contenido** en ella, así que márgenes iguales harían que la
    /// contención —y no el alcance— fuera el factor que decide el resultado de la prueba.
    /// </remarks>
    private async Task<(Persona Persona, ContextoOperativoDto Contexto)> ConContextoAsync(
        HttpClient clienteGlobal,
        Guid principalId)
    {
        var ahora = DateTime.UtcNow;

        var persona = await fixture.Api.SembrarPersonaAsync(
            companiaId: principalId,
            inicioVigencia: ahora.AddMonths(-6),
            finVigencia: ahora.AddYears(2));

        using var respuesta = await clienteGlobal.PostAsJsonAsync(
            new Uri($"/api/personas/{persona.Id}/contextos-operativos", UriKind.Relative),
            new ContextoOperativoRequest(
                principalId, ahora.AddMonths(-1), ahora.AddMonths(6)),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        var contexto = (await respuesta.Content.ReadFromJsonAsync<ContextoOperativoDto>(
            ApiFactory.Json))!;

        return (persona, contexto);
    }

    private async Task<Guid> SembrarTipoCredencialAsync()
    {
        var id = Guid.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var tipo = new TipoCredencial { Nombre = $"Fotocheck {Sufijo()}", Estado = Estado.ACTIVO };
            db.Set<TipoCredencial>().Add(tipo);
            await db.SaveChangesAsync();
            id = tipo.Id;
        });

        return id;
    }

    private static async Task<AsignacionCredencialDto> AsignarCredencialAsync(
        HttpClient clienteGlobal,
        Guid personaId,
        Guid principalId,
        Guid tipoCredencialId)
    {
        var ahora = DateTime.UtcNow;

        using var respuesta = await clienteGlobal.PostAsJsonAsync(
            new Uri($"/api/personas/{personaId}/credenciales", UriKind.Relative),
            // Dentro de la ventana del contexto que `ConContextoAsync` abrió (RF-072).
            new AsignacionCredencialRequest(
                principalId,
                tipoCredencialId,
                ahora.AddDays(-1),
                ahora.AddMonths(3)),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AsignacionCredencialDto>(ApiFactory.Json))!;
    }

    private sealed record PaginaDto<T>(List<T> Items, int Total);

    private sealed record CompaniaResumen(Guid Id, string Nombre);

    private sealed record CredencialResumen(Guid Id);
}
