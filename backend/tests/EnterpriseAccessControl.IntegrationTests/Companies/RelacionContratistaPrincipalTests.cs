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
/// Relaciones Contratista↔Principal: simultaneidad con varias Principales, cierre automático de la
/// previa, finalización explícita y no-solapamiento respaldado por trigger (RF-051, CS-012;
/// research.md §5).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RelacionContratistaPrincipalTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static readonly DateTime Enero = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    private async Task<HttpClient> ClienteConAlcanceAsync(params Guid[] companiaIds)
    {
        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"),
            Password,
            alcanceCompanias: companiaIds);

        return await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    private static Task<HttpResponseMessage> CrearRelacionAsync(
        HttpClient cliente,
        Guid contratistaId,
        Guid principalId,
        DateTime inicio) =>
        cliente.PostAsJsonAsync(
            new Uri($"/api/companias/{contratistaId}/relaciones-principales", UriKind.Relative),
            new RelacionContratistaPrincipalRequest(principalId, inicio),
            ApiFactory.Json);

    private static async Task<IReadOnlyList<RelacionContratistaPrincipalDto>> ListarAsync(
        HttpClient cliente,
        Guid contratistaId) =>
        await cliente.GetFromJsonAsync<IReadOnlyList<RelacionContratistaPrincipalDto>>(
            new Uri($"/api/companias/{contratistaId}/relaciones-principales", UriKind.Relative),
            ApiFactory.Json) ?? [];

    [Fact]
    public async Task Declarar_una_relacion_la_deja_vigente_con_fin_abierto()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Contratista A", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal A");

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, principal.Id);

        using var respuesta = await CrearRelacionAsync(cliente, contratista.Id, principal.Id, Enero);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var creada = await respuesta.Content.ReadFromJsonAsync<RelacionContratistaPrincipalDto>(
            ApiFactory.Json);

        creada!.CompaniaContratistaId.Should().Be(contratista.Id);
        creada.CompaniaPrincipalId.Should().Be(principal.Id);
        creada.FechaHoraInicio.Should().Be(Enero);

        // null = vigencia abierta. RF-071 obliga a informar la fecha de fin en las asociaciones
        // vinculadas a una Persona; ésta vincula dos compañías y conserva esa semántica.
        creada.FechaHoraFin.Should().BeNull();
    }

    [Fact]
    public async Task Una_contratista_puede_tener_relaciones_vigentes_con_varias_principales()
    {
        // CS-012: es el escenario normal de una contratista que presta servicios a varias mineras.
        var contratista = await fixture.Api.SembrarCompaniaAsync("Multi", TipoCompania.CONTRATISTA);
        var p1 = await fixture.Api.SembrarCompaniaAsync("Principal Uno");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Principal Dos");

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, p1.Id, p2.Id);

        using (var primera = await CrearRelacionAsync(cliente, contratista.Id, p1.Id, Enero))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using (var segunda = await CrearRelacionAsync(cliente, contratista.Id, p2.Id, Enero))
        {
            // Mismo rango temporal, Principal distinta: la partición del trigger es el par completo.
            segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var relaciones = await ListarAsync(cliente, contratista.Id);

        relaciones.Should().HaveCount(2);
        relaciones.Should().OnlyContain(r => r.FechaHoraFin == null);
        relaciones.Select(r => r.CompaniaPrincipalId).Should().BeEquivalentTo([p1.Id, p2.Id]);
    }

    [Fact]
    public async Task Declarar_una_nueva_relacion_cierra_automaticamente_la_previa_del_mismo_par()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Renueva", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Renueva");

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, principal.Id);

        using (var primera = await CrearRelacionAsync(cliente, contratista.Id, principal.Id, Enero))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var julio = Enero.AddMonths(6);

        using (var segunda = await CrearRelacionAsync(cliente, contratista.Id, principal.Id, julio))
        {
            segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var relaciones = await ListarAsync(cliente, contratista.Id);

        relaciones.Should().HaveCount(2);

        var vigente = relaciones.Single(r => r.FechaHoraFin is null);
        vigente.FechaHoraInicio.Should().Be(julio);

        var cerrada = relaciones.Single(r => r.FechaHoraFin is not null);
        cerrada.FechaHoraInicio.Should().Be(Enero);

        // La previa se cierra exactamente cuando empieza la nueva: sin hueco y sin solapamiento.
        cerrada.FechaHoraFin.Should().Be(julio);
    }

    [Fact]
    public async Task No_se_puede_iniciar_una_relacion_antes_del_inicio_de_la_vigente()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Retro", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Retro");

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, principal.Id);

        using (var primera = await CrearRelacionAsync(cliente, contratista.Id, principal.Id, Enero))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Cerrar la previa antes de su propio inicio produciría un intervalo invertido.
        using var retroactiva = await CrearRelacionAsync(
            cliente, contratista.Id, principal.Id, Enero.AddMonths(-3));

        retroactiva.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task El_trigger_rechaza_un_solapamiento_insertado_saltandose_la_aplicacion()
    {
        // Se escribe directamente contra la base de datos para simular la condición de carrera que
        // la validación de aplicación no puede evitar por sí sola: dos peticiones concurrentes que
        // superan la comprobación previa antes de que ninguna haya escrito.
        var contratista = await fixture.Api.SembrarCompaniaAsync("Carrera", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Carrera");

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = contratista.Id,
                CompaniaPrincipalId = principal.Id,
                FechaHoraInicio = Enero,
                FechaHoraFin = null,
            });

            await db.SaveChangesAsync();
        });

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = contratista.Id,
                CompaniaPrincipalId = principal.Id,
                FechaHoraInicio = Enero.AddMonths(3),
                FechaHoraFin = null,
            });

            // El trigger AFTER INSERT aborta la transacción: es la última línea de defensa del
            // invariante temporal (Principio IV, research.md §5).
            var guardar = async () => await db.SaveChangesAsync();
            var error = await guardar.Should()
                .ThrowAsync<DbUpdateException>()
                .WithInnerException<DbUpdateException, Microsoft.Data.SqlClient.SqlException>();

            // El mensaje procede del THROW del trigger, no de EF: confirma que quien rechazó la
            // escritura fue la base de datos.
            error.Which.Message.Should().Contain("Solapamiento");
        });
    }

    [Fact]
    public async Task El_trigger_admite_intervalos_contiguos_sin_solapamiento()
    {
        // Frontera: el fin de una y el inicio de la siguiente coinciden. No hay solapamiento, porque
        // el intervalo es semiabierto [inicio, fin).
        var contratista = await fixture.Api.SembrarCompaniaAsync("Contigua", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Contigua");

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = contratista.Id,
                CompaniaPrincipalId = principal.Id,
                FechaHoraInicio = Enero,
                FechaHoraFin = Enero.AddMonths(6),
            });

            await db.SaveChangesAsync();
        });

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().Add(new RelacionContratistaPrincipal
            {
                CompaniaContratistaId = contratista.Id,
                CompaniaPrincipalId = principal.Id,
                FechaHoraInicio = Enero.AddMonths(6),
                FechaHoraFin = null,
            });

            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().NotThrowAsync();
        });
    }

    [Fact]
    public async Task El_trigger_no_interfiere_entre_principales_distintas()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Paralela", TipoCompania.CONTRATISTA);
        var p1 = await fixture.Api.SembrarCompaniaAsync("Paralela Uno");
        var p2 = await fixture.Api.SembrarCompaniaAsync("Paralela Dos");

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<RelacionContratistaPrincipal>().AddRange(
                new RelacionContratistaPrincipal
                {
                    CompaniaContratistaId = contratista.Id,
                    CompaniaPrincipalId = p1.Id,
                    FechaHoraInicio = Enero,
                    FechaHoraFin = null,
                },
                new RelacionContratistaPrincipal
                {
                    CompaniaContratistaId = contratista.Id,
                    CompaniaPrincipalId = p2.Id,
                    FechaHoraInicio = Enero,
                    FechaHoraFin = null,
                });

            // Rangos idénticos pero particiones distintas: el trigger no debe disparar (RF-051).
            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().NotThrowAsync();
        });
    }

    [Fact]
    public async Task Finalizar_una_relacion_vigente_le_fija_fecha_de_fin()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Finaliza", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Finaliza");

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, principal.Id);

        RelacionContratistaPrincipalDto creada;
        using (var alta = await CrearRelacionAsync(cliente, contratista.Id, principal.Id, Enero))
        {
            creada = (await alta.Content.ReadFromJsonAsync<RelacionContratistaPrincipalDto>(
                ApiFactory.Json))!;
        }

        using var respuesta = await cliente.PostAsync(
            new Uri(
                $"/api/companias/{contratista.Id}/relaciones-principales/{creada.Id}/finalizar",
                UriKind.Relative),
            content: null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var relaciones = await ListarAsync(cliente, contratista.Id);
        relaciones.Should().ContainSingle().Which.FechaHoraFin.Should().NotBeNull();
    }

    [Fact]
    public async Task Finalizar_dos_veces_la_misma_relacion_devuelve_409()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Doble fin", TipoCompania.CONTRATISTA);
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal Doble");

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, principal.Id);

        RelacionContratistaPrincipalDto creada;
        using (var alta = await CrearRelacionAsync(cliente, contratista.Id, principal.Id, Enero))
        {
            creada = (await alta.Content.ReadFromJsonAsync<RelacionContratistaPrincipalDto>(
                ApiFactory.Json))!;
        }

        var ruta = new Uri(
            $"/api/companias/{contratista.Id}/relaciones-principales/{creada.Id}/finalizar",
            UriKind.Relative);

        using (var primera = await cliente.PostAsync(ruta, content: null))
        {
            primera.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var segunda = await cliente.PostAsync(ruta, content: null);

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task No_se_puede_declarar_una_relacion_contra_una_compania_que_no_es_principal()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Origen", TipoCompania.CONTRATISTA);
        var otraContratista = await fixture.Api.SembrarCompaniaAsync("Destino", TipoCompania.CONTRATISTA);

        using var cliente = await ClienteConAlcanceAsync(contratista.Id, otraContratista.Id);

        using var respuesta = await CrearRelacionAsync(
            cliente, contratista.Id, otraContratista.Id, Enero);

        // RF-051: el vínculo es Contratista→Principal; entre dos contratistas no existe.
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task La_ruta_exige_que_el_contratistaId_sea_una_contratista()
    {
        var principalComoOrigen = await fixture.Api.SembrarCompaniaAsync("No es contratista");
        var principal = await fixture.Api.SembrarCompaniaAsync("Principal destino");

        using var cliente = await ClienteConAlcanceAsync(principalComoOrigen.Id, principal.Id);

        using var respuesta = await CrearRelacionAsync(
            cliente, principalComoOrigen.Id, principal.Id, Enero);

        // contracts/companies.yaml documenta 404 para este caso: la ruta entera deja de aplicar.
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_se_ven_relaciones_cuya_principal_esta_fuera_del_alcance()
    {
        var contratista = await fixture.Api.SembrarCompaniaAsync("Parcial", TipoCompania.CONTRATISTA);
        var visible = await fixture.Api.SembrarCompaniaAsync("Principal visible");
        var oculta = await fixture.Api.SembrarCompaniaAsync("Principal oculta");

        // El alcance cubre la contratista y una sola de las dos principales.
        using var completo = await ClienteConAlcanceAsync(contratista.Id, visible.Id, oculta.Id);

        using (var a = await CrearRelacionAsync(completo, contratista.Id, visible.Id, Enero))
        {
            a.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using (var b = await CrearRelacionAsync(completo, contratista.Id, oculta.Id, Enero))
        {
            b.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var parcial = await ClienteConAlcanceAsync(contratista.Id, visible.Id);

        var relaciones = await ListarAsync(parcial, contratista.Id);

        // Administrar la Contratista no da acceso a saber con qué otras Principales trabaja.
        relaciones.Should().ContainSingle().Which.CompaniaPrincipalId.Should().Be(visible.Id);
    }
}
