using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// CS-043: registros creados bajo la regla anterior a RF-082 que ya exceden la pertenencia.
/// </summary>
/// <remarks>
/// RF-082 (VF-007) no es retroactiva: estos registros conservan sus fechas, se consultan tal cual y
/// pueden desactivarse o reprogramar sus bloques aunque la persona ya no pertenezca a ninguna compañía.
/// Solo reactivarlos o cambiar su vigencia exige la nueva contención.
///
/// Se siembran **directamente en la base de datos**, sin pasar por los servicios, igual que
/// <c>RevalidacionDinamicaTests</c>: es la única forma de reproducir hoy un dato que la API ya no
/// permitiría crear.
///
/// Los perfiles solo exponen alta y consulta; desactivarlos, cambiar sus fechas o reactivarlos
/// pertenece a VF-008. Aquí se cubre lo que sí existe: su consulta y que no se reescriben.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RegistrosAnterioresRf082Tests(SqlServerFixture fixture)
{
    private sealed record Legado(
        EscenarioPermisos Escenario,
        DateTime Desde,
        DateTime Hasta,
        Guid PerfilId,
        Guid PermisoActivoId,
        Guid PermisoInactivoId);

    /// <summary>
    /// Un perfil y dos permisos PERSONA (uno ACTIVO y otro INACTIVO) que empiezan un año antes y terminan
    /// un año después de la pertenencia vigente.
    /// </summary>
    private async Task<Legado> SembrarLegadoAsync()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        var pertenencia = (await escenario.Us5.PertenenciasEnBaseAsync())[0];

        var inicio = pertenencia.FechaHoraInicio.AddYears(-1);
        var fin = pertenencia.FechaHoraFin.AddYears(1);

        Guid perfilId = Guid.Empty, activoId = Guid.Empty, inactivoId = Guid.Empty;

        await escenario.Us5.ConDatosAsync(async db =>
        {
            var perfil = new AsignacionTipoPersona
            {
                PersonaId = escenario.Persona.Id,
                TipoPersonaId = escenario.TipoPersonaId,
                FechaHoraInicio = inicio,
                FechaHoraFin = fin,
                Estado = Estado.ACTIVO,
            };

            db.Set<AsignacionTipoPersona>().Add(perfil);

            var activo = PermisoLegado(escenario, inicio, fin, Estado.ACTIVO);
            var inactivo = PermisoLegado(escenario, inicio, fin, Estado.INACTIVO);

            db.Set<PermisoAcceso>().AddRange(activo, inactivo);

            foreach (var permiso in new[] { activo, inactivo })
            {
                db.Set<BloqueHorarioPermiso>().Add(new BloqueHorarioPermiso
                {
                    PermisoAccesoId = permiso.Id,
                    DiaSemana = escenario.DiaEvaluado,
                    HoraInicio = new TimeOnly(8, 0),
                    HoraFin = new TimeOnly(17, 0),
                });
            }

            await db.SaveChangesAsync();

            (perfilId, activoId, inactivoId) = (perfil.Id, activo.Id, inactivo.Id);
        });

        return new Legado(
            escenario, pertenencia.FechaHoraInicio, pertenencia.FechaHoraFin, perfilId, activoId, inactivoId);
    }

    private static PermisoAcceso PermisoLegado(
        EscenarioPermisos escenario,
        DateTime inicio,
        DateTime fin,
        Estado estado) => new()
        {
            AreaAccesoId = escenario.Area.Id,
            Alcance = AlcancePermiso.PERSONA,
            PersonaId = escenario.Persona.Id,
            FechaHoraInicioVigencia = inicio,
            FechaHoraFinVigencia = fin,
            Estado = estado,
        };

    // --- (a) Consulta ---------------------------------------------------------------------------------

    [Fact]
    public async Task CS043_a_los_registros_anteriores_se_consultan_con_sus_fechas_originales()
    {
        var legado = await SembrarLegadoAsync();
        var (esperadoInicio, esperadoFin) = (legado.Desde.AddYears(-1), legado.Hasta.AddYears(1));

        var perfiles = await legado.Escenario.Cliente.GetFromJsonAsync<IReadOnlyList<AsignacionTipoPersonaDto>>(
            legado.Escenario.Us5.Ruta("perfiles"), ApiFactory.Json);

        var perfil = perfiles!.Single(p => p.Id == legado.PerfilId);
        perfil.FechaHoraInicio.Should().Be(esperadoInicio);
        perfil.FechaHoraFin.Should().Be(esperadoFin);

        var permiso = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);
        permiso.FechaHoraInicioVigencia.Should().Be(esperadoInicio);
        permiso.FechaHoraFinVigencia.Should().Be(esperadoFin);
        permiso.Estado.Should().Be(Estado.ACTIVO);
    }

    // --- (b) Solo bloques -----------------------------------------------------------------------------

    [Fact]
    public async Task CS043_b_cambiar_solo_los_bloques_se_acepta_y_conserva_las_fechas()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);

        using var respuesta = await PutMismasFechasAsync(legado, actual, Estado.ACTIVO, NuevosBloques(legado));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarFechasIntactasAsync(legado, legado.PermisoActivoId);
    }

    [Fact]
    public async Task CS043_b_reenviar_las_mismas_fechas_civiles_conserva_los_instantes()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);

        // VF-004 (RF-083 (d), F-6): sustituye el caso de tolerancia submilisegundo de research.md §35.3. Con
        // fechas civiles, no cambiar la vigencia es reenviar las mismas fechas, y el servidor conserva los
        // instantes almacenados aunque no sean límites de día.
        using var respuesta = await ContencionPermisosPersonaTests.PutAsync(
            legado.Escenario.Cliente,
            legado.PermisoActivoId,
            legado.Escenario.Peticion(
                AlcancePermiso.PERSONA,
                inicio: actual.FechaInicioVigencia,
                fin: actual.FechaFinVigencia,
                bloques: NuevosBloques(legado)));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarFechasIntactasAsync(legado, legado.PermisoActivoId);
    }

    [Fact]
    public async Task CS043_b_cambiar_solo_los_bloques_se_acepta_aunque_la_persona_ya_no_tenga_pertenencia()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);

        await ContencionPermisosPersonaTests.VencerPertenenciaAsync(legado.Escenario);

        using var respuesta = await PutMismasFechasAsync(legado, actual, Estado.ACTIVO, NuevosBloques(legado));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarFechasIntactasAsync(legado, legado.PermisoActivoId);
    }

    // --- (c) Desactivación ----------------------------------------------------------------------------

    [Fact]
    public async Task CS043_c_desactivar_un_registro_anterior_se_acepta()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);

        using var respuesta = await PutMismasFechasAsync(legado, actual, Estado.INACTIVO);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ObtenerPermisoAsync(legado, legado.PermisoActivoId)).Estado.Should().Be(Estado.INACTIVO);
        await VerificarFechasIntactasAsync(legado, legado.PermisoActivoId);
    }

    [Fact]
    public async Task CS043_c_desactivar_se_acepta_aunque_la_persona_ya_no_tenga_pertenencia()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);

        await ContencionPermisosPersonaTests.VencerPertenenciaAsync(legado.Escenario);

        // Reducir acceso nunca queda bloqueado: desactivar no consulta la pertenencia.
        using var respuesta = await PutMismasFechasAsync(legado, actual, Estado.INACTIVO);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerificarFechasIntactasAsync(legado, legado.PermisoActivoId);
    }

    // --- (d) Reactivación -----------------------------------------------------------------------------

    [Fact]
    public async Task CS043_d_reactivar_un_registro_fuera_de_contencion_se_rechaza()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoInactivoId);

        using var respuesta = await PutMismasFechasAsync(legado, actual, Estado.ACTIVO);

        await ContencionPerfilesTests.VerificarProblemaAsync(
            respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);

        (await ObtenerPermisoAsync(legado, legado.PermisoInactivoId)).Estado.Should().Be(Estado.INACTIVO);
    }

    [Fact]
    public async Task CS043_d_reactivar_sin_pertenencia_vigente_se_rechaza()
    {
        var legado = await SembrarLegadoAsync();
        var actual = await ObtenerPermisoAsync(legado, legado.PermisoInactivoId);

        await ContencionPermisosPersonaTests.VencerPertenenciaAsync(legado.Escenario);

        using var respuesta = await PutMismasFechasAsync(legado, actual, Estado.ACTIVO);

        await ContencionPerfilesTests.VerificarProblemaAsync(
            respuesta, HttpStatusCode.BadRequest, CodigosError.SinPertenenciaVigente);
    }

    // --- (e) Cambio de fechas -------------------------------------------------------------------------

    [Fact]
    public async Task CS043_e_cambiar_las_fechas_a_un_rango_aun_fuera_de_contencion_se_rechaza()
    {
        var legado = await SembrarLegadoAsync();

        // Acortarlo no basta: el rango resultante completo debe caber (sigue empezando antes).
        using var respuesta = await ContencionPermisosPersonaTests.PutAsync(
            legado.Escenario.Cliente,
            legado.PermisoActivoId,
            legado.Escenario.Peticion(
                AlcancePermiso.PERSONA,
                inicio: EscenarioPermisos.FechaDeclarada(legado.Desde).AddMonths(-1),
                fin: EscenarioPermisos.FechaDeclarada(legado.Hasta)));

        await ContencionPerfilesTests.VerificarProblemaAsync(
            respuesta, HttpStatusCode.Conflict, CodigosError.FueraDeContencionTemporal);

        await VerificarFechasIntactasAsync(legado, legado.PermisoActivoId);
    }

    [Fact]
    public async Task CS043_e_cambiar_las_fechas_a_un_rango_contenido_se_acepta()
    {
        var legado = await SembrarLegadoAsync();

        using var respuesta = await ContencionPermisosPersonaTests.PutAsync(
            legado.Escenario.Cliente,
            legado.PermisoActivoId,
            legado.Escenario.Peticion(
                AlcancePermiso.PERSONA,
                inicio: EscenarioPermisos.FechaDeclarada(legado.Desde),
                fin: EscenarioPermisos.FechaDeclarada(legado.Hasta)));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        // Desde VF-004 los extremos que cambian se normalizan a días completos en la zona del área.
        var permiso = await ObtenerPermisoAsync(legado, legado.PermisoActivoId);
        permiso.FechaInicioVigencia.Should().Be(EscenarioPermisos.FechaDeclarada(legado.Desde));
        permiso.FechaFinVigencia.Should().Be(EscenarioPermisos.FechaDeclarada(legado.Hasta));
        permiso.VigenciaEnDiasCompletos.Should().BeTrue();
    }

    // --- (f) Sin reescritura automática ---------------------------------------------------------------

    [Fact]
    public async Task CS043_f_la_regla_no_reescribe_por_si_misma_ningun_registro_existente()
    {
        var legado = await SembrarLegadoAsync();
        var antes = await InstantaneaAsync(legado);

        // Operar sobre la misma persona con la regla nueva no toca lo que ya había.
        var otroPerfil = await ContencionTemporalTests.SembrarTipoPersonaAsync(legado.Escenario.Us5);

        using (var perfil = await legado.Escenario.Us5.AsignarPerfilAsync(otroPerfil, legado.Desde, legado.Hasta))
        {
            perfil.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await legado.Escenario.CrearPermisoAsync(legado.Escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.FechaDeclarada(legado.Desde),
            fin: EscenarioPermisos.FechaDeclarada(legado.Hasta)));

        (await InstantaneaAsync(legado)).Should().BeEquivalentTo(antes);
    }

    // --- Utilidades ------------------------------------------------------------------------------------

    private static IReadOnlyList<BloqueHorarioRequest> NuevosBloques(Legado legado) =>
        [new BloqueHorarioRequest(legado.Escenario.DiaEvaluado, "07:00", "19:00")];

    private static Task<HttpResponseMessage> PutMismasFechasAsync(
        Legado legado,
        PermisoAccesoDto actual,
        Estado estado,
        IReadOnlyList<BloqueHorarioRequest>? bloques = null) =>
        ContencionPermisosPersonaTests.PutAsync(
            legado.Escenario.Cliente,
            actual.Id,
            legado.Escenario.Peticion(
                AlcancePermiso.PERSONA,
                inicio: actual.FechaInicioVigencia,
                fin: actual.FechaFinVigencia,
                estado: estado,
                bloques: bloques));

    private static async Task<PermisoAccesoDto> ObtenerPermisoAsync(Legado legado, Guid id) =>
        (await legado.Escenario.Cliente.GetFromJsonAsync<PermisoAccesoDto>(
            new Uri($"/api/permisos/{id}", UriKind.Relative), ApiFactory.Json))!;

    private static async Task VerificarFechasIntactasAsync(Legado legado, Guid permisoId)
    {
        var permiso = await ObtenerPermisoAsync(legado, permisoId);

        permiso.FechaHoraInicioVigencia.Should().Be(legado.Desde.AddYears(-1));
        permiso.FechaHoraFinVigencia.Should().Be(legado.Hasta.AddYears(1));
    }

    private static async Task<List<object>> InstantaneaAsync(Legado legado)
    {
        List<object> filas = [];

        await legado.Escenario.Us5.ConDatosAsync(async db =>
        {
            var perfil = await db.Set<AsignacionTipoPersona>().AsNoTracking()
                .SingleAsync(p => p.Id == legado.PerfilId);

            filas.Add(new { perfil.FechaHoraInicio, perfil.FechaHoraFin, perfil.Estado, perfil.UpdatedAt });

            foreach (var id in new[] { legado.PermisoActivoId, legado.PermisoInactivoId })
            {
                var permiso = await db.Set<PermisoAcceso>().AsNoTracking().SingleAsync(p => p.Id == id);

                filas.Add(new
                {
                    permiso.FechaHoraInicioVigencia,
                    permiso.FechaHoraFinVigencia,
                    permiso.Estado,
                    permiso.UpdatedAt,
                });
            }
        });

        return filas;
    }
}
