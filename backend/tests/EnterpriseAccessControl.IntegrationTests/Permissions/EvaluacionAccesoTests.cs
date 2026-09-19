using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.AreaAccess;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Flujo completo de evaluación de acceso de extremo a extremo contra SQL Server, con cada motivo de
/// denegación (research.md §7; RF-023, RF-024, RF-059, RF-066).
/// </summary>
/// <remarks>
/// Las pruebas unitarias del evaluador fijan las reglas paso a paso. Aquí se comprueba lo que ellas no
/// pueden: que el orquestador trae de la base de datos exactamente el estado que corresponde a cada
/// paso, que los códigos HTTP son los del contrato y que la evaluación no escribe nada.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class EvaluacionAccesoTests(SqlServerFixture fixture)
{
    private Task<EscenarioPermisos> MontarAsync(bool comoContratista = false) =>
        new EscenarioPermisos(fixture).MontarAsync(comoContratista);

    [Fact]
    public async Task Persona_con_contexto_credencial_perfil_y_permiso_vigentes_obtiene_concedido()
    {
        // Independent Test de la Historia 8.
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.MotivoDenegacion.Should().BeNull();
        resultado.PermisoAplicadoId.Should().Be(permiso.Id);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.PERSONA);
        resultado.CompaniaPrincipalId.Should().Be(escenario.PrincipalA.Id);
        resultado.ContextoOperativoId.Should().Be(escenario.ContextoId);
        resultado.EvaluadoEnZonaHoraria.Should().Be("America/Lima");
    }

    // --- Pasos 1 a 4: 404 del contrato ---------------------------------------------------------------

    [Fact]
    public async Task Una_persona_inexistente_devuelve_404()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostEvaluacionAsync(personaId: Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Un_area_inexistente_devuelve_404()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostEvaluacionAsync(areaId: Guid.CreateVersion7());

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Un_usuario_sin_alcance_sobre_la_principal_del_area_recibe_404()
    {
        // Paso 1 (RF-005, RF-049): fuera de alcance es indistinguible de inexistente.
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        using var ajeno = await fixture.Api.ClienteDeAreasAsync(
            escenario.PrincipalB.Id, escenario.Contratista.Id);

        using var respuesta = await escenario.PostEvaluacionAsync(cliente: ajeno);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Omitir_la_fecha_a_evaluar_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            new Uri("/api/evaluacion-acceso", UriKind.Relative),
            new { personaId = escenario.Persona.Id, areaAccesoId = escenario.Area.Id },
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Paso 5 ------------------------------------------------------------------------------------

    [Fact]
    public async Task Sin_contexto_operativo_con_la_principal_del_area_deniega()
    {
        // La persona tiene contexto y credencial con la Principal A; el área es de la Principal B.
        var escenario = await MontarAsync();

        var areaB = await escenario.CrearAreaAsync(escenario.PrincipalB.Id, "Planta B");
        await escenario.AutorizarPerfilEnAreaAsync(areaB.Id, escenario.TipoPersonaId);
        await escenario.CrearPermisoAsync(escenario.Peticion(AlcancePermiso.PERSONA, areaId: areaB.Id));

        var resultado = await escenario.EvaluarAsync(areaId: areaB.Id);

        // CS-018: un permiso sobre el área de B no basta sin contexto operativo con B.
        resultado.Resultado.Should().Be(ResultadoEvaluacion.DENEGADO);
        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
        resultado.CompaniaPrincipalId.Should().Be(escenario.PrincipalB.Id);
        resultado.ContextoOperativoId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluar_fuera_de_la_vigencia_del_contexto_deniega()
    {
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        // El contexto de EscenarioUs5 empieza hace un mes: dos meses atrás aún no existía.
        var resultado = await escenario.EvaluarAsync(fechaHora: EscenarioPermisos.Instante.AddMonths(-2));

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
    }

    [Fact]
    public async Task Contratista_con_la_relacion_con_la_principal_vencida_deniega_con_su_motivo()
    {
        var escenario = await MontarAsync(comoContratista: true);
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        // La relación termina justo antes del instante evaluado; el contexto sigue escrito y vigente.
        // No se ejercita ninguna cascada: la Decisión Pendiente #6 sigue abierta.
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var relacion = await db.Set<RelacionContratistaPrincipal>().SingleAsync(r =>
                r.CompaniaContratistaId == escenario.Contratista.Id
                && r.CompaniaPrincipalId == escenario.PrincipalA.Id);

            relacion.FechaHoraFin = EscenarioPermisos.Instante.AddHours(-1);
            await db.SaveChangesAsync();
        });

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.RELACION_CONTRATISTA_PRINCIPAL_VENCIDA);
        resultado.ContextoOperativoId.Should().Be(escenario.ContextoId);
    }

    // --- Paso 6 (el detalle de los cuatro casos está en GateCredencialTests) --------------------------

    [Fact]
    public async Task Sin_credencial_para_la_principal_deniega()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync(conCredencial: false);
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    // --- Pasos 7 y 8 -------------------------------------------------------------------------------

    [Fact]
    public async Task Un_area_inactiva_deniega()
    {
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);
        await escenario.DesactivarAreaAsync(escenario.Area.Id, escenario.Area.Nombre);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.AREA_INACTIVA);
    }

    [Fact]
    public async Task Un_area_que_no_autoriza_el_perfil_de_la_persona_deniega()
    {
        var escenario = await MontarAsync();

        var otroPerfil = await fixture.Api.SembrarTipoPersonaAsync();
        await escenario.AutorizarPerfilEnAreaAsync(escenario.Area.Id, otroPerfil);
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERFIL_NO_AUTORIZADO_EN_AREA);
    }

    // --- Pasos 10 a 12 -----------------------------------------------------------------------------

    [Fact]
    public async Task Sin_ningun_permiso_aplicable_deniega()
    {
        var escenario = await MontarAsync();

        // Existe un permiso en el área, pero para otra persona.
        var otra = await fixture.Api.SembrarPersonaAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA, sujetoId: otra.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_PERMISO_APLICABLE);
    }

    [Fact]
    public async Task Un_permiso_vencido_deniega()
    {
        var escenario = await MontarAsync();

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.Instante.AddMonths(-1),
            fin: EscenarioPermisos.Instante.AddHours(-1)));

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERMISO_FUERA_DE_VIGENCIA);
    }

    [Fact]
    public async Task Un_permiso_dado_de_baja_no_concede()
    {
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(escenario.Peticion(AlcancePermiso.PERSONA, estado: Estado.INACTIVO));

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERMISO_FUERA_DE_VIGENCIA);
    }

    [Fact]
    public async Task Fuera_del_bloque_horario_deniega()
    {
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA, bloques: [escenario.BloqueQueNoCubre()]);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.DENEGADO);
        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO);
    }

    [Fact]
    public async Task El_bloque_horario_se_compara_en_hora_de_Lima_y_no_en_UTC()
    {
        // El instante son las 14:00 UTC (09:00 en Lima). Un bloque 13:00–15:00 cubriría la hora UTC,
        // no la local: si el motor comparara en UTC concedería por error.
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(
            AlcancePermiso.PERSONA,
            bloques: [new BloqueHorarioRequest(escenario.DiaEvaluado, "13:00", "15:00")]);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO);
    }

    [Fact]
    public async Task Un_permiso_de_unidad_organizativa_concede_a_quien_tiene_esa_unidad_vigente()
    {
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.UNIDAD_ORGANIZATIVA);
        resultado.PermisoAplicadoId.Should().Be(permiso.Id);
    }

    [Fact]
    public async Task Un_permiso_de_compania_concede_a_quien_pertenece_a_ella()
    {
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(AlcancePermiso.COMPANIA, sujetoId: escenario.PrincipalA.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.COMPANIA);
        resultado.PermisoAplicadoId.Should().Be(permiso.Id);
    }

    // --- Ausencia de efectos --------------------------------------------------------------------------

    [Fact]
    public async Task Evaluar_no_escribe_nada_en_la_base_de_datos()
    {
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA, bloques: [escenario.BloqueQueNoCubre()]);

        var antes = await HuellaAsync(escenario.Persona.Id);

        await escenario.EvaluarAsync();
        await escenario.EvaluarAsync(fechaHora: EscenarioPermisos.Instante.AddYears(5));

        (await HuellaAsync(escenario.Persona.Id)).Should().Be(antes);
    }

    /// <summary>Última modificación de cada asociación de la persona, para detectar escrituras.</summary>
    private async Task<string> HuellaAsync(Guid personaId)
    {
        var huella = string.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var contextos = (await db.Set<ContextoOperativoPersonaPrincipal>().AsNoTracking()
                    .Where(c => c.PersonaId == personaId).ToListAsync())
                .Select(c => $"C{c.Id}:{c.UpdatedAt:O}:{c.Estado}:{c.FechaHoraFin:O}");

            var credenciales = (await db.Set<AsignacionCredencial>().AsNoTracking()
                    .Where(c => c.PersonaId == personaId).ToListAsync())
                .Select(c => $"K{c.Id}:{c.UpdatedAt:O}:{c.Estado}:{c.FechaHoraFin:O}");

            var pertenencias = (await db.Set<AsignacionPersonaCompania>().AsNoTracking()
                    .Where(a => a.PersonaId == personaId).ToListAsync())
                .Select(a => $"P{a.Id}:{a.UpdatedAt:O}:{a.Estado}:{a.FechaHoraFin:O}");

            huella = string.Join("|", contextos.Concat(credenciales).Concat(pertenencias).Order());
        });

        return huella;
    }
}
