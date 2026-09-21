using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Companies;

/// <summary>
/// CS-041: el cambio de <c>TipoCompañía</c> se rechaza mientras existan dependencias incompatibles,
/// y ninguna de ellas se modifica (RF-081).
/// </summary>
/// <remarks>
/// La verificación es **simétrica**: una Contratista con relaciones vigentes como Contratista queda
/// igual de bloqueada para pasar a PRINCIPAL_MANDANTE que una Principal con áreas para pasar a
/// CONTRATISTA. Nunca hay cascada: el sistema cuenta, informa y deja decidir a un humano.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class CambioTipoCompaniaTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private async Task<HttpClient> ClienteGlobalAsync()
    {
        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"), Password, global: true);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    private static CompaniaRequest Peticion(Compania compania, TipoCompania tipoDestino) =>
        new(
            compania.Nombre,
            compania.TipoDocumentoId,
            compania.NumeroDocumento,
            tipoDestino,
            compania.Estado,
            tipoDestino == TipoCompania.PRINCIPAL_MANDANTE ? "America/Lima" : null);

    private static Task<HttpResponseMessage> CambiarTipoAsync(
        HttpClient cliente,
        Compania compania,
        TipoCompania destino) =>
        cliente.PutAsJsonAsync(
            new Uri($"/api/companias/{compania.Id}", UriKind.Relative),
            Peticion(compania, destino),
            ApiFactory.Json);

    [Fact]
    public async Task CS041_sin_dependencias_el_cambio_se_acepta_en_ambas_direcciones()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Sin Dependencias");

        using var cliente = await ClienteGlobalAsync();

        using var aContratista = await CambiarTipoAsync(cliente, compania, TipoCompania.CONTRATISTA);
        aContratista.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizada = await fixture.Api.SembrarCompaniaAsync("Vuelta", TipoCompania.CONTRATISTA);

        using var aPrincipal = await CambiarTipoAsync(
            cliente, actualizada, TipoCompania.PRINCIPAL_MANDANTE);

        aPrincipal.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CS041_un_area_de_acceso_bloquea_el_paso_a_contratista()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Con Área");

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<AreaAcceso>().Add(new AreaAcceso
            {
                Nombre = $"Planta {Guid.CreateVersion7():N}"[..20],
                CompaniaPrincipalId = principal.Id,
                Estado = Estado.ACTIVO,
            });

            await db.SaveChangesAsync();
        });

        using var cliente = await ClienteGlobalAsync();

        using var respuesta = await CambiarTipoAsync(cliente, principal, TipoCompania.CONTRATISTA);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);
        problema!.Codigo.Should().Be("CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS");
        problema.Detail.Should().Contain("Áreas de acceso");

        // Ninguna dependencia se modifica: el rechazo no dispara cascada alguna (RF-081).
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var areas = await db.Set<AreaAcceso>()
                .CountAsync(a => a.CompaniaPrincipalId == principal.Id);

            areas.Should().Be(1);

            var sinCambiar = await db.Set<Compania>().AsNoTracking()
                .SingleAsync(c => c.Id == principal.Id);

            sinCambiar.TipoCompania.Should().Be(TipoCompania.PRINCIPAL_MANDANTE);
        });
    }

    [Fact]
    public async Task CS041_una_raiz_de_unidad_organizativa_bloquea_el_paso_a_contratista()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Con Raíz");

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var raiz = new UnidadOrganizativa { Nombre = "Raíz", Estado = Estado.ACTIVO };
            db.Set<UnidadOrganizativa>().Add(raiz);

            db.Set<CompaniaPrincipalUnidadOrganizativaRaiz>().Add(
                new CompaniaPrincipalUnidadOrganizativaRaiz
                {
                    CompaniaId = principal.Id,
                    UnidadOrganizativaRaizId = raiz.Id,
                });

            await db.SaveChangesAsync();
        });

        using var cliente = await ClienteGlobalAsync();

        using var respuesta = await CambiarTipoAsync(cliente, principal, TipoCompania.CONTRATISTA);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);
        problema!.Detail.Should().Contain("Raíces de unidad organizativa");
    }

    [Fact]
    public async Task CS041_una_relacion_vigente_bloquea_simetricamente_en_ambas_direcciones()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Relacionada");
        var contratista = await fixture.Api.SembrarCompaniaAsync(
            "Contratista Relacionada", TipoCompania.CONTRATISTA);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = contratista.Id,
                CompaniaPrincipalId = principal.Id,
                FechaHoraInicio = DateTime.UtcNow.AddDays(-1),
                FechaHoraFin = null,
            });

            await db.SaveChangesAsync();
        });

        using var cliente = await ClienteGlobalAsync();

        // Dirección 1: la Principal no puede degradarse mientras actúe como Principal.
        using var principalABajar = await CambiarTipoAsync(
            cliente, principal, TipoCompania.CONTRATISTA);

        principalABajar.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problemaPrincipal = await principalABajar.Content
            .ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);

        problemaPrincipal!.Detail.Should().Contain("Relaciones vigentes como Principal");

        // Dirección 2 (simetría de D6): la Contratista tampoco puede ascender.
        using var contratistaASubir = await CambiarTipoAsync(
            cliente, contratista, TipoCompania.PRINCIPAL_MANDANTE);

        contratistaASubir.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problemaContratista = await contratistaASubir.Content
            .ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);

        problemaContratista!.Detail.Should().Contain("Relaciones vigentes como Contratista");
    }

    [Fact]
    public async Task CS041_un_contexto_operativo_bloquea_el_paso_a_contratista()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Con Contexto");
        var persona = await fixture.Api.SembrarPersonaAsync(companiaId: principal.Id);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var ahora = DateTime.UtcNow;

            db.Set<ContextoOperativoPersonaPrincipal>().Add(new ContextoOperativoPersonaPrincipal
            {
                PersonaId = persona.Id,
                CompaniaPrincipalId = principal.Id,
                FechaHoraInicio = ahora.AddDays(-1),
                FechaHoraFin = ahora.AddYears(1),
            });

            await db.SaveChangesAsync();
        });

        using var cliente = await ClienteGlobalAsync();

        using var respuesta = await CambiarTipoAsync(cliente, principal, TipoCompania.CONTRATISTA);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);
        problema!.Detail.Should().Contain("Contextos operativos");
    }

    [Fact]
    public async Task CS041_una_credencial_bloquea_el_paso_a_contratista()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Con Credencial");
        var persona = await fixture.Api.SembrarPersonaAsync(companiaId: principal.Id);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var ahora = DateTime.UtcNow;

            // TipoCredencial no se siembra en la migración de maestros (sus valores dependen del
            // despliegue), así que la prueba crea el suyo.
            var tipo = new TipoCredencial
            {
                Nombre = $"Fotocheck {Guid.CreateVersion7():N}"[..30],
                Estado = Estado.ACTIVO,
            };

            db.Set<TipoCredencial>().Add(tipo);

            db.Set<AsignacionCredencial>().Add(new AsignacionCredencial
            {
                PersonaId = persona.Id,
                CompaniaPrincipalId = principal.Id,
                TipoCredencialId = tipo.Id,
                FechaHoraInicio = ahora.AddDays(-1),
                FechaHoraFin = ahora.AddYears(1),
                Estado = EstadoCredencial.ASIGNADO,
            });

            await db.SaveChangesAsync();
        });

        using var cliente = await ClienteGlobalAsync();

        using var respuesta = await CambiarTipoAsync(cliente, principal, TipoCompania.CONTRATISTA);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaDto>(ApiFactory.Json);
        problema!.Detail.Should().Contain("Credenciales");
    }

    [Fact]
    public async Task Una_principal_exige_zona_horaria_iana_valida()
    {
        // RF-080: obligatoria para PRINCIPAL_MANDANTE y validada por request, no solo al arrancar.
        var compania = await fixture.Api.SembrarCompaniaAsync("Zona Inválida");

        using var cliente = await ClienteGlobalAsync();

        using var invalida = await cliente.PutAsJsonAsync(
            new Uri($"/api/companias/{compania.Id}", UriKind.Relative),
            new CompaniaRequest(
                compania.Nombre,
                compania.TipoDocumentoId,
                compania.NumeroDocumento,
                TipoCompania.PRINCIPAL_MANDANTE,
                Estado.ACTIVO,
                "No/Existe"),
            ApiFactory.Json);

        invalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var ausente = await cliente.PutAsJsonAsync(
            new Uri($"/api/companias/{compania.Id}", UriKind.Relative),
            new CompaniaRequest(
                compania.Nombre,
                compania.TipoDocumentoId,
                compania.NumeroDocumento,
                TipoCompania.PRINCIPAL_MANDANTE,
                Estado.ACTIVO,
                null),
            ApiFactory.Json);

        ausente.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Proyección mínima de ProblemDetails con la extensión propia <c>codigo</c>.</summary>
    private sealed record ProblemaDto(string? Detail, string? Codigo);
}
