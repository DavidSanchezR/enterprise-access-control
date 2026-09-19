using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.AreaAccess;

/// <summary>
/// Tipos de persona autorizados por área: asociación y reemplazo del conjunto (Historia 7, RF-019).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AreaAccesoTipoPersonaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Un_area_recien_creada_no_autoriza_ningun_tipo_de_persona()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Nueva");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);

        // Default-deny (Principio I): un área no autoriza a nadie mientras no se declare quién.
        (await cliente.TiposPersonaDeAsync(area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Asociar_dos_tipos_de_persona_los_devuelve_a_ambos()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Asocia");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);

        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();
        var visitante = await fixture.Api.SembrarTipoPersonaAsync();

        using var respuesta = await cliente.ReemplazarTiposPersonaAsync(area.Id, trabajador, visitante);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await cliente.TiposPersonaDeAsync(area.Id))
            .Should().BeEquivalentTo([trabajador, visitante]);
    }

    [Fact]
    public async Task Reemplazar_el_conjunto_deja_solo_los_tipos_indicados()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Reemplaza");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);

        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();
        var visitante = await fixture.Api.SembrarTipoPersonaAsync();
        var proveedor = await fixture.Api.SembrarTipoPersonaAsync();

        using (var _ = await cliente.ReemplazarTiposPersonaAsync(area.Id, trabajador, visitante))
        {
            // Estado de partida.
        }

        // Escenario del Independent Test de la Historia 7: el PUT reemplaza, no acumula.
        using var respuesta = await cliente.ReemplazarTiposPersonaAsync(area.Id, proveedor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await cliente.TiposPersonaDeAsync(area.Id)).Should().BeEquivalentTo([proveedor]);
    }

    [Fact]
    public async Task Un_conjunto_vacio_retira_todas_las_autorizaciones()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Vacía");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);
        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();

        using (var _ = await cliente.ReemplazarTiposPersonaAsync(area.Id, trabajador))
        {
            // Estado de partida.
        }

        using var respuesta = await cliente.ReemplazarTiposPersonaAsync(area.Id);

        // Vaciar es una petición legítima —el área deja de admitir perfiles—, no un error.
        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await cliente.TiposPersonaDeAsync(area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Repetir_un_tipo_en_la_peticion_no_lo_duplica()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Duplica");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);
        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();

        using var respuesta = await cliente.ReemplazarTiposPersonaAsync(
            area.Id, trabajador, trabajador);

        // El índice único lo impediría con un 500; el servicio deduplica antes de escribir.
        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await cliente.TiposPersonaDeAsync(area.Id)).Should().BeEquivalentTo([trabajador]);
    }

    [Fact]
    public async Task Un_reemplazo_conserva_la_auditoria_de_los_tipos_que_siguen_asociados()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Auditoría");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);

        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();
        var visitante = await fixture.Api.SembrarTipoPersonaAsync();

        using (var _ = await cliente.ReemplazarTiposPersonaAsync(area.Id, trabajador))
        {
            // Estado de partida.
        }

        var idOriginal = Guid.Empty;
        await fixture.Api.ConDbContextAsync(async db =>
        {
            idOriginal = (await db.Set<AreaAccesoTipoPersona>().AsNoTracking()
                .SingleAsync(t => t.AreaAccesoId == area.Id && t.TipoPersonaId == trabajador)).Id;
        });

        using (var _ = await cliente.ReemplazarTiposPersonaAsync(area.Id, trabajador, visitante))
        {
            // Trabajador sigue; visitante se incorpora.
        }

        // La fila de Trabajador es la misma: borrarla y reinsertarla haría parecer que la
        // autorización es nueva (Principio III).
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var fila = await db.Set<AreaAccesoTipoPersona>().AsNoTracking()
                .SingleAsync(t => t.AreaAccesoId == area.Id && t.TipoPersonaId == trabajador);

            fila.Id.Should().Be(idOriginal);
        });
    }

    [Fact]
    public async Task Un_tipo_de_persona_inactivo_no_puede_autorizarse()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Inactivo");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);
        var retirado = await fixture.Api.SembrarTipoPersonaAsync(estado: Estado.INACTIVO);

        using var respuesta = await cliente.ReemplazarTiposPersonaAsync(area.Id, retirado);

        // RF-032: un valor maestro inactivo no entra en asignaciones nuevas.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be("VALOR_MAESTRO_INACTIVO");

        (await cliente.TiposPersonaDeAsync(area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Un_tipo_de_persona_inexistente_se_rechaza_sin_tocar_el_conjunto()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Inexistente");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var area = await cliente.CrearRaizAsync("Planta", principal.Id);
        var valido = await fixture.Api.SembrarTipoPersonaAsync();

        using (var _ = await cliente.ReemplazarTiposPersonaAsync(area.Id, valido))
        {
            // Estado de partida.
        }

        using var respuesta = await cliente.ReemplazarTiposPersonaAsync(
            area.Id, valido, Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Se valida antes de escribir: un rechazo parcial dejaría el área en un estado que nadie pidió.
        (await cliente.TiposPersonaDeAsync(area.Id)).Should().BeEquivalentTo([valido]);
    }

    [Fact]
    public async Task Cada_area_mantiene_su_propio_conjunto_de_tipos()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Independiente");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        var raiz = await cliente.CrearRaizAsync("Planta", principal.Id);
        var hija = await cliente.CrearHijaAsync("Molienda", raiz.Id);

        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();
        var visitante = await fixture.Api.SembrarTipoPersonaAsync();

        using (var _ = await cliente.ReemplazarTiposPersonaAsync(raiz.Id, trabajador, visitante))
        {
            // Solo sobre la raíz.
        }

        // La autorización no se hereda por la jerarquía: RF-019 la declara por área, y suponer
        // herencia ampliaría en silencio quién puede entrar a un sector interior.
        (await cliente.TiposPersonaDeAsync(hija.Id)).Should().BeEmpty();
        (await cliente.TiposPersonaDeAsync(raiz.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task No_se_pueden_consultar_los_tipos_de_un_area_fuera_de_alcance()
    {
        var ajena = await fixture.Api.SembrarCompaniaAsync("Tipos Principal ajena");
        var mia = await fixture.Api.SembrarCompaniaAsync("Tipos Principal propia");

        using var duena = await fixture.Api.ClienteDeAreasAsync(ajena.Id);
        var area = await duena.CrearRaizAsync("Planta reservada", ajena.Id);

        using var intruso = await fixture.Api.ClienteDeAreasAsync(mia.Id);

        using var respuesta = await intruso.GetAsync(
            new Uri($"/api/areas-acceso/{area.Id}/tipos-persona", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_se_pueden_reemplazar_los_tipos_de_un_area_fuera_de_alcance()
    {
        var ajena = await fixture.Api.SembrarCompaniaAsync("Tipos ajena escritura");
        var mia = await fixture.Api.SembrarCompaniaAsync("Tipos propia escritura");

        using var duena = await fixture.Api.ClienteDeAreasAsync(ajena.Id);
        var area = await duena.CrearRaizAsync("Planta reservada", ajena.Id);

        var trabajador = await fixture.Api.SembrarTipoPersonaAsync();

        using var intruso = await fixture.Api.ClienteDeAreasAsync(mia.Id);

        using var respuesta = await intruso.ReemplazarTiposPersonaAsync(area.Id, trabajador);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Y el área ajena sigue sin autorizaciones: el rechazo no escribió nada.
        (await duena.TiposPersonaDeAsync(area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Los_tipos_de_un_area_inexistente_devuelven_404()
    {
        var principal = await fixture.Api.SembrarCompaniaAsync("Tipos Área Fantasma");
        using var cliente = await fixture.Api.ClienteDeAreasAsync(principal.Id);

        using var respuesta = await cliente.GetAsync(
            new Uri($"/api/areas-acceso/{Guid.CreateVersion7()}/tipos-persona", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
