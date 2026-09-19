using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// RF-050: el alcance administrativo (<c>AlcanceUsuarioCompañía</c>) es independiente de la relación
/// operacional Persona → Compañía → UnidadOrganizativa.
/// </summary>
/// <remarks>
/// <c>Usuario</c> y <c>Persona</c> son entidades distintas: el usuario opera el sistema, la persona es
/// el sujeto cuyo acceso físico se evalúa. Que hoy no exista todavía la entidad <c>Persona</c> (llega
/// en una fase posterior) no debilita estas pruebas: la comprobación estructural sobre el modelo de EF
/// Core fallará en el momento en que alguien conecte ambas jerarquías, que es exactamente cuando debe
/// avisar.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class AlcanceIndependienteTests(SqlServerFixture fixture)
{
    private const string Password = "Contrasena1Segura";

    private static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    [Fact]
    public void El_alcance_administrativo_no_tiene_ninguna_relacion_con_entidades_operacionales()
    {
        using var ambito = fixture.Api.Services.CreateScope();
        var db = ambito.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var tipo = db.Model.FindEntityType(typeof(AlcanceUsuarioCompania));
        tipo.Should().NotBeNull();

        var relacionados = tipo!.GetForeignKeys()
            .Select(fk => fk.PrincipalEntityType.ClrType.Name)
            .ToList();

        // Sólo Usuario y Compañía: ninguna arista hacia la jerarquía operacional.
        relacionados.Should().BeEquivalentTo(["Usuario", "Compania"]);

        relacionados.Should().NotContain(
            nombre => nombre.Contains("Persona", StringComparison.Ordinal),
            "el alcance administrativo no debe depender de la relación operacional de una persona");

        relacionados.Should().NotContain(
            nombre => nombre.Contains("UnidadOrganizativa", StringComparison.Ordinal));
    }

    [Fact]
    public void La_entidad_Usuario_no_referencia_a_ninguna_entidad_operacional()
    {
        using var ambito = fixture.Api.Services.CreateScope();
        var db = ambito.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var usuario = db.Model.FindEntityType(typeof(Usuario));
        usuario.Should().NotBeNull();

        usuario!.GetForeignKeys().Should().BeEmpty(
            "Usuario es una entidad autónoma; su vínculo con compañías vive en AlcanceUsuarioCompañía");

        usuario.GetNavigations().Should().BeEmpty();
    }

    [Fact]
    public async Task Un_usuario_opera_con_su_alcance_sin_que_exista_ninguna_entidad_operacional_suya()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Minera Independiente");

        var usuario = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("independiente"),
            Password,
            alcanceCompanias: [compania.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, usuario.Correo);
        using var respuesta = await cliente.GetAsync(new Uri("/api/usuarios", UriKind.Relative));

        // No hay ninguna Persona, AsignaciónPersonaCompañía ni UnidadOrganizativa asociada a este
        // usuario, y aun así administra con normalidad.
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Un_usuario_sin_alcance_queda_denegado_por_defecto()
    {
        var usuario = await fixture.Api.SembrarUsuarioAsync(CorreoUnico("sinalcance"), Password);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, usuario.Correo);
        using var respuesta = await cliente.GetAsync(new Uri("/api/usuarios", UriKind.Relative));

        // Principio I: sin Succeed explícito la política CompaniaScope falla.
        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_alcance_se_reemplaza_por_completo_y_solo_afecta_a_ese_usuario()
    {
        var companiaA = await fixture.Api.SembrarCompaniaAsync("Compañía A");
        var companiaB = await fixture.Api.SembrarCompaniaAsync("Compañía B");
        var companiaC = await fixture.Api.SembrarCompaniaAsync("Compañía C");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"),
            Password,
            alcanceCompanias: [companiaA.Id]);

        var otro = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("otro"),
            Password,
            alcanceCompanias: [companiaA.Id, companiaB.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var reemplazo = await cliente.PutAsJsonAsync(
            new Uri($"/api/usuarios/{otro.Id}/alcance-companias", UriKind.Relative),
            new ReemplazarAlcanceRequest([companiaC.Id]));

        reemplazo.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await fixture.Api.AlcanceDeAsync(otro.Id)).Should().BeEquivalentTo([companiaC.Id]);

        // El alcance del administrador que ejecutó el cambio no se ve afectado.
        (await fixture.Api.AlcanceDeAsync(admin.Id)).Should().BeEquivalentTo([companiaA.Id]);
    }

    [Fact]
    public async Task No_se_puede_incorporar_al_alcance_una_compania_inactiva()
    {
        var activa = await fixture.Api.SembrarCompaniaAsync("Activa");
        var inactiva = await fixture.Api.SembrarCompaniaAsync(
            "Inactiva",
            estado: Domain.Enums.Estado.INACTIVO);

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"),
            Password,
            alcanceCompanias: [activa.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        using var respuesta = await cliente.PutAsJsonAsync(
            new Uri($"/api/usuarios/{admin.Id}/alcance-companias", UriKind.Relative),
            new ReemplazarAlcanceRequest([inactiva.Id]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // El alcance previo se conserva intacto: el rechazo no deja al usuario a medias.
        (await fixture.Api.AlcanceDeAsync(admin.Id)).Should().BeEquivalentTo([activa.Id]);
    }

    [Fact]
    public async Task Dos_usuarios_pueden_compartir_exactamente_el_mismo_alcance()
    {
        // El alcance administrativo no es exclusivo: varias personas del área de seguridad pueden
        // administrar la misma compañía. Es otra diferencia con la relación operacional.
        var compania = await fixture.Api.SembrarCompaniaAsync("Compartida");

        var uno = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("uno"), Password, alcanceCompanias: [compania.Id]);
        var dos = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("dos"), Password, alcanceCompanias: [compania.Id]);

        (await fixture.Api.AlcanceDeAsync(uno.Id)).Should().BeEquivalentTo([compania.Id]);
        (await fixture.Api.AlcanceDeAsync(dos.Id)).Should().BeEquivalentTo([compania.Id]);
    }

    [Fact]
    public async Task Una_compania_no_puede_repetirse_dentro_del_alcance_de_un_mismo_usuario()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Sin Duplicados");

        var usuario = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("dup"), Password, alcanceCompanias: [compania.Id]);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<AlcanceUsuarioCompania>().Add(new AlcanceUsuarioCompania
            {
                UsuarioId = usuario.Id,
                CompaniaId = compania.Id,
            });

            // Lo impide el índice único compuesto, no sólo el código de aplicación.
            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().ThrowAsync<DbUpdateException>();
        });
    }
}
