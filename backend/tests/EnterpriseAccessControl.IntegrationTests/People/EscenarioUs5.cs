using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Construcción del escenario compartido por las pruebas de la Historia 5.
/// </summary>
/// <remarks>
/// Casi todas parten del mismo montaje —una persona con pertenencia vigente, uno o dos contextos
/// operativos, sus unidades y credenciales—, así que armarlo vive aquí en lugar de repetirse. Cada
/// prueba siembra sus propias compañías y su propio usuario: comparten la base de datos del
/// contenedor, no los datos.
/// </remarks>
internal sealed class EscenarioUs5(SqlServerFixture fixture)
{
    public const string Password = "Contrasena1Segura";

    /// <summary>Ventana amplia por defecto, para que la contención nunca sea el factor limitante.</summary>
    public static DateTime InicioPertenencia => DateTime.UtcNow.AddMonths(-1);

    public static DateTime FinPertenencia => DateTime.UtcNow.AddYears(1);

    public static string Sufijo() => Guid.CreateVersion7().ToString("N")[..12];

    public HttpClient Cliente { get; private set; } = null!;

    public Compania Contratista { get; private set; } = null!;

    public Compania PrincipalA { get; private set; } = null!;

    public Compania PrincipalB { get; private set; } = null!;

    public Persona Persona { get; private set; } = null!;

    public Guid TipoCredencialId { get; private set; }

    /// <summary>
    /// Monta compañías, usuario y persona. La persona queda **sin** pertenencia: cada prueba decide
    /// cómo y cuándo crearla.
    /// </summary>
    public async Task<EscenarioUs5> MontarAsync(bool conRelacionB = true)
    {
        var sufijo = Sufijo();

        Contratista = await fixture.Api.SembrarCompaniaAsync(
            $"Contratista {sufijo}", TipoCompania.CONTRATISTA);

        PrincipalA = await fixture.Api.SembrarCompaniaAsync($"Principal A {sufijo}");
        PrincipalB = await fixture.Api.SembrarCompaniaAsync($"Principal B {sufijo}");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"us5.{Guid.CreateVersion7():N}@empresa.cl",
            Password,
            alcanceCompanias: [Contratista.Id, PrincipalA.Id, PrincipalB.Id]);

        Cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        Persona = await fixture.Api.SembrarPersonaAsync(apellidos: $"US5{sufijo}");

        // Relaciones Contratista↔Principal, que son las que habilitan el Caso B (RF-054).
        await SembrarRelacionAsync(Contratista.Id, PrincipalA.Id);

        if (conRelacionB)
        {
            await SembrarRelacionAsync(Contratista.Id, PrincipalB.Id);
        }

        TipoCredencialId = await SembrarTipoCredencialAsync();

        return this;
    }

    public async Task SembrarRelacionAsync(Guid contratistaId, Guid principalId)
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = contratistaId,
                CompaniaPrincipalId = principalId,
                FechaHoraInicio = DateTime.UtcNow.AddMonths(-2),
                FechaHoraFin = null,
            });

            await db.SaveChangesAsync();
        });
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

    /// <summary>Crea un árbol de unidades bajo la Principal indicada y devuelve el nodo raíz.</summary>
    public async Task<Guid> SembrarUnidadAsync(Guid companiaPrincipalId, string nombre = "Gerencia")
    {
        var raizId = Guid.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var unidad = new UnidadOrganizativa { Nombre = $"{nombre} {Sufijo()}" };
            db.Set<UnidadOrganizativa>().Add(unidad);

            db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>().Add(
                new CompaniaPrincipalUnidadOrganizativaRaiz
                {
                    CompaniaId = companiaPrincipalId,
                    UnidadOrganizativaRaizId = unidad.Id,
                });

            await db.SaveChangesAsync();
            raizId = unidad.Id;
        });

        return raizId;
    }

    // --- Llamadas a la API ----------------------------------------------------------------------

    public Task<HttpResponseMessage> CrearPertenenciaAsync(
        Guid companiaId,
        DateTime? inicio = null,
        DateTime? fin = null) =>
        Cliente.PostAsJsonAsync(
            Ruta("historial-companias"),
            new AsignacionCompaniaRequest(
                companiaId, inicio ?? InicioPertenencia, fin ?? FinPertenencia),
            ApiFactory.Json);

    public async Task<AsignacionCompaniaDto> PertenenciaVigenteAsync(
        Guid? companiaId = null,
        DateTime? inicio = null,
        DateTime? fin = null)
    {
        using var respuesta = await CrearPertenenciaAsync(
            companiaId ?? Contratista.Id, inicio, fin);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AsignacionCompaniaDto>(ApiFactory.Json))!;
    }

    public Task<HttpResponseMessage> FinalizarAsync(Guid asignacionId, DateTime fechaHoraFin) =>
        Cliente.PostAsJsonAsync(
            Ruta($"historial-companias/{asignacionId}/finalizar"),
            new FinalizarPertenenciaRequest(fechaHoraFin),
            ApiFactory.Json);

    public Task<HttpResponseMessage> RenovarAsync(Guid asignacionId, DateTime fechaHoraFin) =>
        Cliente.PostAsJsonAsync(
            Ruta($"historial-companias/{asignacionId}/renovar"),
            new RenovarPertenenciaRequest(fechaHoraFin),
            ApiFactory.Json);

    public Task<HttpResponseMessage> AbrirContextoAsync(
        Guid companiaPrincipalId,
        DateTime? inicio = null,
        DateTime? fin = null) =>
        Cliente.PostAsJsonAsync(
            Ruta("contextos-operativos"),
            new ContextoOperativoRequest(
                companiaPrincipalId, inicio ?? InicioPertenencia, fin ?? FinPertenencia),
            ApiFactory.Json);

    public async Task<ContextoOperativoDto> ContextoAsync(
        Guid companiaPrincipalId,
        DateTime? inicio = null,
        DateTime? fin = null)
    {
        using var respuesta = await AbrirContextoAsync(companiaPrincipalId, inicio, fin);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<ContextoOperativoDto>(ApiFactory.Json))!;
    }

    public Task<HttpResponseMessage> AsignarUnidadAsync(
        Guid contextoId,
        Guid unidadId,
        DateTime? inicio = null,
        DateTime? fin = null) =>
        Cliente.PostAsJsonAsync(
            Ruta($"contextos-operativos/{contextoId}/unidad-organizativa"),
            new AsignacionUnidadOrganizativaRequest(
                unidadId, inicio ?? InicioPertenencia, fin ?? FinPertenencia),
            ApiFactory.Json);

    public Task<HttpResponseMessage> AsignarPerfilAsync(
        Guid tipoPersonaId,
        DateTime? inicio = null,
        DateTime? fin = null) =>
        Cliente.PostAsJsonAsync(
            Ruta("perfiles"),
            new AsignacionTipoPersonaRequest(
                tipoPersonaId, inicio ?? InicioPertenencia, fin ?? FinPertenencia),
            ApiFactory.Json);

    public async Task<EstadoEfectivoPersonaDto> EstadoEfectivoAsync(DateTime fechaHora)
    {
        var ruta = new Uri(
            $"/api/personas/{Persona.Id}/estado-efectivo?fechaHora={fechaHora:O}",
            UriKind.Relative);

        return (await Cliente.GetFromJsonAsync<EstadoEfectivoPersonaDto>(ruta, ApiFactory.Json))!;
    }

    /// <summary>Envía el alta de una credencial por la API sin exigir que tenga éxito.</summary>
    /// <remarks>
    /// Hasta la Historia 9 el alta se invocaba directamente por el servicio porque no existía
    /// controlador. Desde que lo hay, pasa por HTTP: así cada prueba que asigna credenciales ejercita
    /// también el alcance y los códigos de error de contracts/credentials.yaml.
    /// </remarks>
    public Task<HttpResponseMessage> PostCredencialAsync(
        Guid companiaPrincipalId,
        DateTime? inicio = null,
        DateTime? fin = null,
        Guid? tipoCredencialId = null) =>
        Cliente.PostAsJsonAsync(
            Ruta("credenciales"),
            new AsignacionCredencialRequest(
                companiaPrincipalId,
                tipoCredencialId ?? TipoCredencialId,
                inicio ?? InicioPertenencia,
                fin ?? FinPertenencia),
            ApiFactory.Json);

    public async Task<AsignacionCredencialDto> AsignarCredencialAsync(
        Guid companiaPrincipalId,
        DateTime? inicio = null,
        DateTime? fin = null)
    {
        using var respuesta = await PostCredencialAsync(companiaPrincipalId, inicio, fin);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AsignacionCredencialDto>(ApiFactory.Json))!;
    }

    public Uri Ruta(string sufijo) =>
        new($"/api/personas/{Persona.Id}/{sufijo}", UriKind.Relative);

    // --- Lectura directa de datos ---------------------------------------------------------------

    public Task ConDatosAsync(Func<Infrastructure.Persistence.AppDbContext, Task> accion) =>
        fixture.Api.ConDbContextAsync(accion);

    public async Task<List<ContextoOperativoPersonaPrincipal>> ContextosEnBaseAsync()
    {
        List<ContextoOperativoPersonaPrincipal> resultado = [];

        await ConDatosAsync(async db => resultado = await db.Set<ContextoOperativoPersonaPrincipal>()
            .AsNoTracking()
            .Where(c => c.PersonaId == Persona.Id)
            .ToListAsync());

        return resultado;
    }

    public async Task<List<AsignacionPersonaUnidadOrganizativa>> UnidadesEnBaseAsync()
    {
        List<AsignacionPersonaUnidadOrganizativa> resultado = [];

        await ConDatosAsync(async db => resultado = await db.Set<AsignacionPersonaUnidadOrganizativa>()
            .AsNoTracking()
            .Where(u => u.PersonaId == Persona.Id)
            .ToListAsync());

        return resultado;
    }

    public async Task<List<AsignacionCredencial>> CredencialesEnBaseAsync()
    {
        List<AsignacionCredencial> resultado = [];

        await ConDatosAsync(async db => resultado = await db.Set<AsignacionCredencial>()
            .AsNoTracking()
            .Where(c => c.PersonaId == Persona.Id)
            .ToListAsync());

        return resultado;
    }

    public async Task<List<AsignacionPersonaCompania>> PertenenciasEnBaseAsync()
    {
        List<AsignacionPersonaCompania> resultado = [];

        await ConDatosAsync(async db => resultado = await db.Set<AsignacionPersonaCompania>()
            .AsNoTracking()
            .Where(a => a.PersonaId == Persona.Id)
            .OrderBy(a => a.FechaHoraInicio)
            .ToListAsync());

        return resultado;
    }
}
