using System.Net;
using System.Net.Http.Json;
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
/// Trazabilidad de una secuencia de cambios: quién y cuándo en cada paso
/// (Historia 10; RF-026, RF-027, RF-061, RF-073; research.md §6, §14.2).
/// </summary>
/// <remarks>
/// El modelo de auditoría vigente (research.md §6) registra por entidad la creación y la última
/// actualización; no mantiene un log de cada cambio. La cadena se reconstruye por tanto observando la
/// entidad después de cada escritura: en cada paso <c>UpdatedById</c> debe identificar a quien lo hizo,
/// <c>UpdatedAt</c> debe avanzar y la autoría de la creación no debe moverse nunca.
///
/// Las consultas transversales de auditoría (RF-067) no tienen tareas generadas (tasks.md, Notas) y no
/// se prueban aquí.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class TrazabilidadTests(SqlServerFixture fixture)
{
    private sealed record Usuario(Guid Id, HttpClient Cliente);

    private sealed record Marca(Guid? UpdatedById, DateTime UpdatedAt);

    [Fact]
    public async Task La_cadena_de_ediciones_de_una_persona_identifica_a_cada_autor_en_orden()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync($"Trazabilidad personas {Sufijo()}");
        var (u1, u2, u3) = await TresUsuariosAsync(compania.Id);

        var persona = await fixture.Api.SembrarPersonaAsync(companiaId: compania.Id);
        var peticion = PeticionDesde(await LeerAsync<Persona>(persona.Id));

        var cadena = new List<Marca>();

        foreach (var (autor, nombres) in new[] { (u1, "Uno"), (u2, "Dos"), (u3, "Tres") })
        {
            var antes = DateTime.UtcNow;

            using var respuesta = await autor.Cliente.PutAsJsonAsync(
                new Uri($"/api/personas/{persona.Id}", UriKind.Relative),
                peticion with { Nombres = nombres },
                ApiFactory.Json);

            respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

            cadena.Add(await VerificarPasoAsync<Persona>(persona.Id, autor.Id, antes));
        }

        VerificarCadena(cadena, [u1.Id, u2.Id, u3.Id]);

        // La creación la hizo la siembra, sin usuario; ninguna edición posterior la reescribe.
        (await LeerAsync<Persona>(persona.Id)).CreatedById.Should().BeNull();
    }

    [Fact]
    public async Task La_cadena_de_cambios_de_un_permiso_identifica_a_cada_autor_en_orden()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var (u1, u2, u3) = await TresUsuariosAsync(escenario.PrincipalA.Id, escenario.Contratista.Id);

        var antesAlta = DateTime.UtcNow;

        using var alta = await u1.Cliente.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative),
            escenario.Peticion(AlcancePermiso.PERSONA),
            ApiFactory.Json);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var permiso = (await alta.Content.ReadFromJsonAsync<PermisoAccesoDto>(ApiFactory.Json))!;

        var cadena = new List<Marca> { await VerificarPasoAsync<PermisoAcceso>(permiso.Id, u1.Id, antesAlta) };

        foreach (var (autor, estado) in new[] { (u2, Estado.INACTIVO), (u3, Estado.ACTIVO) })
        {
            var antes = DateTime.UtcNow;

            using var cambio = await autor.Cliente.PutAsJsonAsync(
                new Uri($"/api/permisos/{permiso.Id}", UriKind.Relative),
                escenario.Peticion(AlcancePermiso.PERSONA, estado: estado),
                ApiFactory.Json);

            cambio.StatusCode.Should().Be(HttpStatusCode.OK);

            cadena.Add(await VerificarPasoAsync<PermisoAcceso>(permiso.Id, autor.Id, antes));
        }

        VerificarCadena(cadena, [u1.Id, u2.Id, u3.Id]);
        (await LeerAsync<PermisoAcceso>(permiso.Id)).CreatedById.Should().Be(u1.Id);
    }

    [Fact]
    public async Task Renovacion_y_revocacion_en_cascada_quedan_atribuidas_a_quien_las_registro()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        var (u1, u2, u3) = await TresUsuariosAsync(
            escenario.Contratista.Id, escenario.PrincipalA.Id, escenario.PrincipalB.Id);

        // Paso 1 (u1): pertenencia, contexto, unidad y credencial.
        using var pertenenciaAlta = await u1.Cliente.PostAsJsonAsync(
            escenario.Ruta("historial-companias"),
            new AsignacionCompaniaRequest(
                escenario.Contratista.Id, EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia),
            ApiFactory.Json);
        pertenenciaAlta.StatusCode.Should().Be(HttpStatusCode.Created);
        var pertenencia = (await pertenenciaAlta.Content.ReadFromJsonAsync<AsignacionCompaniaDto>(ApiFactory.Json))!;

        using var contextoAlta = await u1.Cliente.PostAsJsonAsync(
            escenario.Ruta("contextos-operativos"),
            new ContextoOperativoRequest(
                escenario.PrincipalA.Id, EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia),
            ApiFactory.Json);
        contextoAlta.StatusCode.Should().Be(HttpStatusCode.Created);
        var contexto = (await contextoAlta.Content.ReadFromJsonAsync<ContextoOperativoDto>(ApiFactory.Json))!;

        var unidadId = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);
        using var unidadAlta = await u1.Cliente.PostAsJsonAsync(
            escenario.Ruta($"contextos-operativos/{contexto.Id}/unidad-organizativa"),
            new AsignacionUnidadOrganizativaRequest(
                unidadId, EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia),
            ApiFactory.Json);
        unidadAlta.StatusCode.Should().Be(HttpStatusCode.Created);
        var unidad = (await unidadAlta.Content.ReadFromJsonAsync<AsignacionUnidadOrganizativaDto>(ApiFactory.Json))!;

        using var credencialAlta = await u1.Cliente.PostAsJsonAsync(
            escenario.Ruta("credenciales"),
            new AsignacionCredencialRequest(
                escenario.PrincipalA.Id, escenario.TipoCredencialId,
                EscenarioUs5.InicioPertenencia, EscenarioUs5.FinPertenencia),
            ApiFactory.Json);
        credencialAlta.StatusCode.Should().Be(HttpStatusCode.Created);
        var credencial = (await credencialAlta.Content.ReadFromJsonAsync<AsignacionCredencialDto>(ApiFactory.Json))!;

        var tras1Contexto = await MarcaAsync<ContextoOperativoPersonaPrincipal>(contexto.Id);
        tras1Contexto.UpdatedById.Should().Be(u1.Id);

        // Paso 2 (u2): renovación (RF-073). Solo escribe la pertenencia; no toca a sus dependientes.
        var antesRenovar = DateTime.UtcNow;

        using (var renovada = await u2.Cliente.PostAsJsonAsync(
                   escenario.Ruta($"historial-companias/{pertenencia.Id}/renovar"),
                   new RenovarPertenenciaRequest(DateTime.UtcNow.AddYears(2)),
                   ApiFactory.Json))
        {
            renovada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var tras2Pertenencia =
            await VerificarPasoAsync<AsignacionPersonaCompania>(pertenencia.Id, u2.Id, antesRenovar);

        (await MarcaAsync<ContextoOperativoPersonaPrincipal>(contexto.Id))
            .Should().Be(tras1Contexto, "renovar no reescribe los dependientes");

        // Paso 3 (u3): cese de la pertenencia. La cascada escribe cada dependiente dentro de la misma
        // operación, y cada fila queda atribuida a quien registró el cese (research.md §14.2).
        var antesCese = DateTime.UtcNow;

        using (var cese = await u3.Cliente.PostAsJsonAsync(
                   escenario.Ruta($"historial-companias/{pertenencia.Id}/finalizar"),
                   new FinalizarPertenenciaRequest(DateTime.UtcNow),
                   ApiFactory.Json))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var tras3Pertenencia =
            await VerificarPasoAsync<AsignacionPersonaCompania>(pertenencia.Id, u3.Id, antesCese);

        VerificarCadena([tras2Pertenencia, tras3Pertenencia], [u2.Id, u3.Id]);

        var contextoRevocado = await VerificarPasoAsync<ContextoOperativoPersonaPrincipal>(contexto.Id, u3.Id, antesCese);
        var unidadRevocada = await VerificarPasoAsync<AsignacionPersonaUnidadOrganizativa>(unidad.Id, u3.Id, antesCese);
        var credencialRevocada = await VerificarPasoAsync<AsignacionCredencial>(credencial.Id, u3.Id, antesCese);

        contextoRevocado.UpdatedAt.Should().BeAfter(tras1Contexto.UpdatedAt);

        // Premisa: fueron escrituras de la cascada, no de otra operación.
        (await LeerAsync<ContextoOperativoPersonaPrincipal>(contexto.Id)).RevocadoPorPertenenciaId.Should().Be(pertenencia.Id);
        (await LeerAsync<AsignacionPersonaUnidadOrganizativa>(unidad.Id)).RevocadoPorPertenenciaId.Should().Be(pertenencia.Id);
        (await LeerAsync<AsignacionCredencial>(credencial.Id)).Estado.Should().Be(EstadoCredencial.REVOCADA);

        // Todo lo que produjo el cese comparte el mismo "cuándo" que la pertenencia cerrada.
        foreach (var marca in new[] { contextoRevocado, unidadRevocada, credencialRevocada })
        {
            marca.UpdatedAt.Should().BeCloseTo(tras3Pertenencia.UpdatedAt, TimeSpan.FromSeconds(5));
        }

        // Y la autoría de la creación sigue siendo de u1 en todas las filas.
        (await LeerAsync<AsignacionPersonaCompania>(pertenencia.Id)).CreatedById.Should().Be(u1.Id);
        (await LeerAsync<ContextoOperativoPersonaPrincipal>(contexto.Id)).CreatedById.Should().Be(u1.Id);
        (await LeerAsync<AsignacionPersonaUnidadOrganizativa>(unidad.Id)).CreatedById.Should().Be(u1.Id);
        (await LeerAsync<AsignacionCredencial>(credencial.Id)).CreatedById.Should().Be(u1.Id);
    }

    // --- Verificaciones ------------------------------------------------------------------------------

    /// <summary>Tras una escritura: el autor es el esperado y el instante cae dentro de la llamada.</summary>
    private async Task<Marca> VerificarPasoAsync<T>(Guid id, Guid autor, DateTime antesDeLaLlamada)
        where T : EntidadBase
    {
        var marca = await MarcaAsync<T>(id);

        marca.UpdatedById.Should().Be(autor, "{0} {1}: debe figurar quien hizo el último cambio", typeof(T).Name, id);

        // datetime2(3) redondea al milisegundo: se admite ese margen en el límite inferior.
        marca.UpdatedAt.Should().BeOnOrAfter(antesDeLaLlamada.AddMilliseconds(-2));
        marca.UpdatedAt.Should().BeOnOrBefore(DateTime.UtcNow);

        return marca;
    }

    private static void VerificarCadena(IReadOnlyList<Marca> cadena, IReadOnlyList<Guid> autores)
    {
        cadena.Select(m => m.UpdatedById).Should().Equal(autores.Select(a => (Guid?)a));
        cadena.Select(m => m.UpdatedAt).Should().BeInAscendingOrder();
        cadena.Select(m => m.UpdatedAt).Should().OnlyHaveUniqueItems("cada paso deja su propio instante");
    }

    // --- Montaje --------------------------------------------------------------------------------------

    private async Task<(Usuario, Usuario, Usuario)> TresUsuariosAsync(params Guid[] companias)
    {
        async Task<Usuario> CrearAsync(string alias)
        {
            var usuario = await fixture.Api.SembrarUsuarioAsync(
                $"traza.{alias}.{Guid.CreateVersion7():N}@empresa.cl",
                "Contrasena1Segura",
                alcanceCompanias: companias);

            return new Usuario(usuario.Id, await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, usuario.Correo));
        }

        return (await CrearAsync("u1"), await CrearAsync("u2"), await CrearAsync("u3"));
    }

    private async Task<Marca> MarcaAsync<T>(Guid id)
        where T : EntidadBase
    {
        var entidad = await LeerAsync<T>(id);
        return new Marca(entidad.UpdatedById, entidad.UpdatedAt);
    }

    private async Task<T> LeerAsync<T>(Guid id)
        where T : EntidadBase
    {
        T? entidad = null;

        await fixture.Api.ConDbContextAsync(async db =>
            entidad = await db.Set<T>().AsNoTracking().SingleAsync(e => e.Id == id));

        return entidad!;
    }

    private static PersonaRequest PeticionDesde(Persona p) =>
        new(
            p.Nombres, p.Apellidos, p.FechaNacimiento, p.TipoDocumentoId, p.NumeroDocumento, p.GeneroId,
            p.CorreoElectronico, p.TipoSangreId, p.ContactoEmergencia, p.NumeroEmergencia);

    private static string Sufijo() => Guid.CreateVersion7().ToString("N")[..12];
}
