using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.Auth;

/// <summary>
/// RF-050: el alcance administrativo (<c>AsignaciónRolAdministrativo</c>) es independiente de la
/// relación operacional Persona → Compañía → UnidadOrganizativa.
/// </summary>
/// <remarks>
/// <c>Usuario</c> y <c>Persona</c> son entidades distintas: el usuario opera el sistema, la persona es
/// el sujeto cuyo acceso físico se evalúa. La comprobación estructural sobre el modelo de EF Core
/// fallará en el momento en que alguien conecte ambas jerarquías, que es exactamente cuando debe
/// avisar.
///
/// Desde la Sesión 2026-09-20 (D1) el alcance dejó de ser un join plano Usuario×Compañía y pasó a ser
/// una asignación de rol con vigencia (RF-074): estas pruebas se actualizaron a la entidad nueva
/// conservando intacto lo que verifican.
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

        var tipo = db.Model.FindEntityType(typeof(AsignacionRolAdministrativo));
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
            "Usuario es una entidad autónoma; su vínculo con compañías vive en AsignaciónRolAdministrativo");

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
    public async Task Un_administrador_global_opera_sin_enumerar_ninguna_compania()
    {
        // RF-074: el alcance GLOBAL no se expresa como lista de compañías. Bajo el modelo anterior
        // este usuario habría sido denegado por tener el alcance "vacío".
        var usuario = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("global"),
            Password,
            global: true);

        var (rol, companiaIds) = await fixture.Api.AlcanceDeAsync(usuario.Id);

        rol.Should().Be(RolAdministrativo.GLOBAL_ADMINISTRATOR);
        companiaIds.Should().BeEmpty();

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(usuario.Id, usuario.Correo);
        using var respuesta = await cliente.GetAsync(new Uri("/api/usuarios", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Asignar_un_rol_agrega_una_asignacion_y_no_reemplaza_las_existentes()
    {
        var companiaA = await fixture.Api.SembrarCompaniaAsync("Compañía A");
        var companiaB = await fixture.Api.SembrarCompaniaAsync("Compañía B");

        // Solo un GLOBAL_ADMINISTRATOR puede asignar en una compañía que no es la suya (RF-076).
        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, global: true);

        var otro = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("otro"), Password, alcanceCompanias: [companiaA.Id]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        var ahora = DateTime.UtcNow;

        using var asignacion = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{otro.Id}/roles", UriKind.Relative),
            new AsignarRolRequest(
                RolAdministrativo.COMPANY_ADMINISTRATOR,
                companiaB.Id,
                ahora.AddDays(-1),
                ahora.AddYears(1)),
            ApiFactory.Json);

        asignacion.StatusCode.Should().Be(HttpStatusCode.Created);

        // La asignación previa sigue vigente: se agregó, no se reemplazó (RF-074, UX-19).
        var (_, companiaIds) = await fixture.Api.AlcanceDeAsync(otro.Id);
        companiaIds.Should().BeEquivalentTo([companiaA.Id, companiaB.Id]);

        // El alcance del administrador que ejecutó el cambio no se ve afectado.
        var (rolAdmin, _) = await fixture.Api.AlcanceDeAsync(admin.Id);
        rolAdmin.Should().Be(RolAdministrativo.GLOBAL_ADMINISTRATOR);
    }

    [Fact]
    public async Task No_se_puede_asignar_un_rol_sobre_una_compania_inactiva()
    {
        var inactiva = await fixture.Api.SembrarCompaniaAsync("Inactiva", estado: Estado.INACTIVO);

        var admin = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("admin"), Password, global: true);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        var ahora = DateTime.UtcNow;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri($"/api/usuarios/{admin.Id}/roles", UriKind.Relative),
            new AsignarRolRequest(
                RolAdministrativo.COMPANY_ADMINISTRATOR,
                inactiva.Id,
                ahora.AddDays(-1),
                ahora.AddYears(1)),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // El alcance previo se conserva intacto: el rechazo no deja al usuario a medias.
        var (rol, _) = await fixture.Api.AlcanceDeAsync(admin.Id);
        rol.Should().Be(RolAdministrativo.GLOBAL_ADMINISTRATOR);
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

        (await fixture.Api.AlcanceDeAsync(uno.Id)).CompaniaIds.Should().BeEquivalentTo([compania.Id]);
        (await fixture.Api.AlcanceDeAsync(dos.Id)).CompaniaIds.Should().BeEquivalentTo([compania.Id]);
    }

    [Fact]
    public async Task Dos_asignaciones_solapadas_del_mismo_par_usuario_compania_las_rechaza_la_base_de_datos()
    {
        var compania = await fixture.Api.SembrarCompaniaAsync("Sin Solapamiento");

        var usuario = await fixture.Api.SembrarUsuarioAsync(
            CorreoUnico("dup"), Password, alcanceCompanias: [compania.Id]);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var ahora = DateTime.UtcNow;

            db.Set<AsignacionRolAdministrativo>().Add(new AsignacionRolAdministrativo
            {
                UsuarioId = usuario.Id,
                Rol = RolAdministrativo.COMPANY_ADMINISTRATOR,
                CompaniaId = compania.Id,
                // Se solapa con la que sembró SembrarUsuarioAsync.
                FechaHoraInicio = ahora,
                FechaHoraFin = ahora.AddMonths(6),
            });

            // Lo impide el trigger de no-solapamiento (RF-075), no sólo el código de aplicación.
            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().ThrowAsync<DbUpdateException>();
        });
    }
}
