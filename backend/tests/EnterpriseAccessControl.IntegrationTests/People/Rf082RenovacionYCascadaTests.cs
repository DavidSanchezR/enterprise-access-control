using System.Net;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.Permissions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// RF-082 frente a la renovación (RF-073) y la cascada de cese (RF-061 a RF-065).
/// </summary>
/// <remarks>
/// Cambio de requisito post-Baseline VF-007. La contención del perfil y de los permisos PERSONA es una
/// restricción de escritura, no una dependencia: renovar la pertenencia amplía el límite para lo que se
/// registre después sin tocar lo existente (como CS-035 para las tres asociaciones de RF-072), y cerrar
/// la pertenencia no los revoca (D3). La cascada sigue alcanzando solo al contexto operativo, la unidad
/// organizativa y la credencial.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class Rf082RenovacionYCascadaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Renovar_la_pertenencia_amplia_el_limite_sin_modificar_perfiles_ni_permisos_existentes()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var us5 = escenario.Us5;

        var pertenencia = (await us5.PertenenciasEnBaseAsync())[0];

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraInicio),
            fin: EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraFin)));

        var tipo = await ContencionTemporalTests.SembrarTipoPersonaAsync(us5);
        var nuevoFin = pertenencia.FechaHoraFin.AddDays(60);

        // Antes de renovar, ese fin excede la pertenencia.
        using (var antes = await us5.AsignarPerfilAsync(tipo, pertenencia.FechaHoraInicio, nuevoFin))
        {
            await ContencionPerfilesTests.VerificarProblemaAsync(
                antes, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);
        }

        var existentes = await InstantaneaAsync(us5);

        using (var renovacion = await us5.RenovarAsync(pertenencia.Id, nuevoFin))
        {
            renovacion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Lo que ya existía no cambia: la renovación no extiende ninguna asociación.
        (await InstantaneaAsync(us5)).Should().BeEquivalentTo(existentes);

        var renovada = (await us5.PertenenciasEnBaseAsync())[0];

        using (var perfil = await us5.AsignarPerfilAsync(tipo, renovada.FechaHoraInicio, renovada.FechaHoraFin))
        {
            perfil.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var permiso = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.FechaDeclarada(renovada.FechaHoraInicio),
            fin: EscenarioPermisos.FechaDeclarada(renovada.FechaHoraFin)));

        permiso.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Finalizar_la_pertenencia_no_revoca_perfiles_ni_permisos_persona()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var us5 = escenario.Us5;

        var pertenencia = (await us5.PertenenciasEnBaseAsync())[0];

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraInicio),
            fin: EscenarioPermisos.FechaDeclarada(pertenencia.FechaHoraFin)));

        var antes = await InstantaneaAsync(us5);

        using (var cese = await us5.FinalizarAsync(pertenencia.Id, DateTime.UtcNow))
        {
            cese.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // D3: RF-082 contiene al perfil y al permiso PERSONA, pero no los hace dependientes.
        (await InstantaneaAsync(us5)).Should().BeEquivalentTo(antes);

        // La cascada de RF-061 sigue alcanzando a sus tres dependientes, sin cambios.
        (await us5.ContextosEnBaseAsync())
            .Should().OnlyContain(c => c.RevocadoPorPertenenciaId == pertenencia.Id);

        (await us5.UnidadesEnBaseAsync())
            .Should().OnlyContain(u => u.RevocadoPorPertenenciaId == pertenencia.Id);

        (await us5.CredencialesEnBaseAsync())
            .Should().OnlyContain(c => c.Estado == EstadoCredencial.REVOCADA);
    }

    /// <summary>Fechas y estado de los perfiles y permisos PERSONA de la persona.</summary>
    private static async Task<List<object>> InstantaneaAsync(EscenarioUs5 us5)
    {
        List<object> filas = [];

        await us5.ConDatosAsync(async db =>
        {
            var perfiles = await db.Set<AsignacionTipoPersona>().AsNoTracking()
                .Where(p => p.PersonaId == us5.Persona.Id)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.FechaHoraInicio, p.FechaHoraFin, p.Estado })
                .ToListAsync();

            var permisos = await db.Set<PermisoAcceso>().AsNoTracking()
                .Where(p => p.PersonaId == us5.Persona.Id)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.FechaHoraInicioVigencia, p.FechaHoraFinVigencia, p.Estado })
                .ToListAsync();

            filas.AddRange(perfiles);
            filas.AddRange(permisos);
        });

        return filas;
    }
}
