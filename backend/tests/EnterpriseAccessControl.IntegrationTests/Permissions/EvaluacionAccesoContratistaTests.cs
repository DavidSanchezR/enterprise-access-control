using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Personal de una compañía contratista accediendo a un área de la Principal a la que presta
/// servicios (Historia 8; RF-054, RF-059; research.md §12).
/// </summary>
/// <remarks>
/// La persona pertenece a la Contratista, tiene contexto operativo con la Principal A —habilitado por
/// la relación Contratista→Principal vigente—, una unidad organizativa de A dentro de ese contexto y
/// una credencial emitida por A.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class EvaluacionAccesoContratistaTests(SqlServerFixture fixture)
{
    private Task<EscenarioPermisos> MontarAsync(bool conCredencial = true) =>
        new EscenarioPermisos(fixture).MontarAsync(comoContratista: true, conCredencial);

    [Fact]
    public async Task Con_permiso_de_unidad_organizativa_de_la_principal_obtiene_concedido()
    {
        // Escenario exacto de T140.
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.UNIDAD_ORGANIZATIVA);
        resultado.PermisoAplicadoId.Should().Be(permiso.Id);
        resultado.CompaniaPrincipalId.Should().Be(escenario.PrincipalA.Id);
        resultado.ContextoOperativoId.Should().Be(escenario.ContextoId);
    }

    [Fact]
    public async Task Un_permiso_de_compania_sobre_la_contratista_concede_a_su_personal()
    {
        // research.md §12: el nivel COMPAÑÍA se resuelve con la compañía de pertenencia vigente, que
        // aquí es la Contratista.
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(
            AlcancePermiso.COMPANIA, sujetoId: escenario.Contratista.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.COMPANIA);
        resultado.PermisoAplicadoId.Should().Be(permiso.Id);
    }

    [Fact]
    public async Task Un_permiso_de_compania_sobre_la_principal_no_alcanza_al_personal_de_la_contratista()
    {
        // Trabajar para la Principal no es pertenecer a ella: el permiso de la plantilla propia de A
        // no se extiende a quien la contratista emplea.
        var escenario = await MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.COMPANIA, sujetoId: escenario.PrincipalA.Id);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_PERMISO_APLICABLE);
    }

    [Fact]
    public async Task Sin_credencial_de_la_principal_el_contratista_es_denegado()
    {
        var escenario = await MontarAsync(conCredencial: false);
        await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA);

        var resultado = await escenario.EvaluarAsync();

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Fact]
    public async Task El_acceso_a_la_principal_A_no_se_extiende_a_un_area_de_la_principal_B()
    {
        // La contratista tiene relación con A y con B, pero la persona solo tiene contexto con A.
        var escenario = await MontarAsync();

        var areaB = await escenario.CrearAreaAsync(escenario.PrincipalB.Id, "Planta B");
        await escenario.AutorizarPerfilEnAreaAsync(areaB.Id, escenario.TipoPersonaId);
        await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.COMPANIA, sujetoId: escenario.Contratista.Id, areaId: areaB.Id));

        var resultado = await escenario.EvaluarAsync(areaId: areaB.Id);

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
    }
}
