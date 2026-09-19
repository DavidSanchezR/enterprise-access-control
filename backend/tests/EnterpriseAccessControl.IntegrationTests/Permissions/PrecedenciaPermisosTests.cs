using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Precedencia PERSONA &gt; UNIDAD_ORGANIZATIVA &gt; COMPAÑÍA con permisos aplicables a la vez en los
/// tres niveles (RF-025; research.md §7 paso 13).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PrecedenciaPermisosTests(SqlServerFixture fixture)
{
    private sealed record TresNiveles(
        EscenarioPermisos Escenario,
        PermisoAccesoDto Persona,
        PermisoAccesoDto Unidad,
        PermisoAccesoDto Compania);

    private async Task<TresNiveles> MontarTresNivelesAsync()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();

        // Se crean en orden inverso a la precedencia: si ganara el primero o el último insertado, la
        // prueba lo delataría.
        var compania = await escenario.CrearPermisoAsync(
            AlcancePermiso.COMPANIA, sujetoId: escenario.PrincipalA.Id);
        var unidad = await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA);
        var persona = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        return new TresNiveles(escenario, persona, unidad, compania);
    }

    [Fact]
    public async Task Con_los_tres_niveles_aplicables_gana_el_permiso_de_persona()
    {
        var montaje = await MontarTresNivelesAsync();

        var resultado = await montaje.Escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.PERSONA);
        resultado.PermisoAplicadoId.Should().Be(montaje.Persona.Id);
    }

    [Fact]
    public async Task Sin_permiso_de_persona_gana_el_de_unidad_sobre_el_de_compania()
    {
        var montaje = await MontarTresNivelesAsync();
        await DarDeBajaAsync(montaje.Escenario, montaje.Persona, AlcancePermiso.PERSONA);

        var resultado = await montaje.Escenario.EvaluarAsync();

        resultado.NivelAplicado.Should().Be(AlcancePermiso.UNIDAD_ORGANIZATIVA);
        resultado.PermisoAplicadoId.Should().Be(montaje.Unidad.Id);
    }

    [Fact]
    public async Task Solo_con_permiso_de_compania_concede_por_ese_nivel()
    {
        var montaje = await MontarTresNivelesAsync();
        await DarDeBajaAsync(montaje.Escenario, montaje.Persona, AlcancePermiso.PERSONA);
        await DarDeBajaAsync(montaje.Escenario, montaje.Unidad, AlcancePermiso.UNIDAD_ORGANIZATIVA);

        var resultado = await montaje.Escenario.EvaluarAsync();

        resultado.NivelAplicado.Should().Be(AlcancePermiso.COMPANIA);
        resultado.PermisoAplicadoId.Should().Be(montaje.Compania.Id);
    }

    [Fact]
    public async Task La_precedencia_no_rescata_un_permiso_de_persona_fuera_de_su_horario()
    {
        // La precedencia desempata entre permisos que ya superaron vigencia y horario; no convierte
        // en aplicable uno que no lo es.
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();

        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA, bloques: [escenario.BloqueQueNoCubre()]);
        var unidad = await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.UNIDAD_ORGANIZATIVA);
        resultado.PermisoAplicadoId.Should().Be(unidad.Id);
    }

    [Fact]
    public async Task La_precedencia_no_rescata_un_permiso_de_persona_vencido()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();

        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.Instante.AddMonths(-2),
            fin: EscenarioPermisos.Instante.AddDays(-1)));

        var compania = await escenario.CrearPermisoAsync(
            AlcancePermiso.COMPANIA, sujetoId: escenario.PrincipalA.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.NivelAplicado.Should().Be(AlcancePermiso.COMPANIA);
        resultado.PermisoAplicadoId.Should().Be(compania.Id);
    }

    [Fact]
    public async Task Un_permiso_de_unidad_no_aplica_a_quien_tiene_otra_unidad()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();

        var otraUnidad = await escenario.Us5.SembrarUnidadAsync(escenario.PrincipalA.Id, "Otra gerencia");
        await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA, sujetoId: otraUnidad);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_PERMISO_APLICABLE);
    }

    [Fact]
    public async Task La_eleccion_es_determinista_entre_evaluaciones_repetidas()
    {
        var montaje = await MontarTresNivelesAsync();

        var resultados = new List<Guid?>();
        for (var i = 0; i < 5; i++)
        {
            resultados.Add((await montaje.Escenario.EvaluarAsync()).PermisoAplicadoId);
        }

        resultados.Should().OnlyContain(id => id == montaje.Persona.Id);
    }

    private static async Task DarDeBajaAsync(
        EscenarioPermisos escenario,
        PermisoAccesoDto permiso,
        AlcancePermiso alcance)
    {
        using var respuesta = await escenario.Cliente.PutAsJsonAsync(
            new Uri($"/api/permisos/{permiso.Id}", UriKind.Relative),
            escenario.Peticion(
                alcance,
                sujetoId: permiso.PersonaId ?? permiso.UnidadOrganizativaId ?? permiso.CompaniaId,
                estado: Estado.INACTIVO),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
