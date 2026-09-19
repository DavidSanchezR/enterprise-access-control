using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad de los endpoints de histórico, contexto operativo y renovación de
/// <c>contracts/people.yaml</c> (Historia 5, RF-014, RF-052 a RF-055, RF-061, RF-073).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class HistorialPersonaContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("people.yaml");

    private static readonly (string Ruta, string Metodo)[] OperacionesUs5 =
    [
        ("/api/personas/{id}/estado-efectivo", "get"),
        ("/api/personas/{id}/historial-companias", "get"),
        ("/api/personas/{id}/historial-companias", "post"),
        ("/api/personas/{id}/historial-companias/{asignacionId}/finalizar", "post"),
        ("/api/personas/{id}/historial-companias/{asignacionId}/renovar", "post"),
        ("/api/personas/{id}/contextos-operativos", "get"),
        ("/api/personas/{id}/contextos-operativos", "post"),
        ("/api/personas/{id}/contextos-operativos/{contextoId}/unidad-organizativa", "get"),
        ("/api/personas/{id}/contextos-operativos/{contextoId}/unidad-organizativa", "post"),
        ("/api/personas/{id}/perfiles", "get"),
        ("/api/personas/{id}/perfiles", "post"),
    ];

    [Fact]
    public void Toda_operacion_del_contrato_existe_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Fact]
    public void La_api_no_expone_operaciones_de_persona_fuera_del_contrato()
    {
        var enLaApi = fixture.DocumentoApi.Paths()
            .EnumerateObject()
            .Where(p => p.Name.StartsWith("/api/personas", StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Select(m => (Ruta: p.Name, Metodo: m.Name)))
            .ToList();

        // Las credenciales cuelgan de /api/personas pero las declara contracts/credentials.yaml
        // (Historia 9): la superficie legítima es la unión de ambos contratos, y nada más.
        var credenciales = ApiContratoFixture.CargarContrato("credentials.yaml").Operaciones;

        enLaApi.Should().BeEquivalentTo(Contrato.Operaciones.Concat(credenciales));
    }

    [Theory]
    [InlineData("/api/personas/{id}/historial-companias", "get")]
    [InlineData("/api/personas/{id}/historial-companias", "post")]
    [InlineData("/api/personas/{id}/historial-companias/{asignacionId}/finalizar", "post")]
    [InlineData("/api/personas/{id}/historial-companias/{asignacionId}/renovar", "post")]
    [InlineData("/api/personas/{id}/contextos-operativos", "get")]
    [InlineData("/api/personas/{id}/contextos-operativos", "post")]
    [InlineData("/api/personas/{id}/contextos-operativos/{contextoId}/unidad-organizativa", "post")]
    [InlineData("/api/personas/{id}/perfiles", "get")]
    [InlineData("/api/personas/{id}/estado-efectivo", "get")]
    public void La_api_declara_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void Los_esquemas_de_historial_y_contexto_coinciden_en_sus_propiedades()
    {
        foreach (var (enContrato, enApi) in new Dictionary<string, string>(StringComparer.Ordinal)
                 {
                     ["AsignacionCompania"] = "AsignacionCompaniaDto",
                     ["AsignacionCompaniaRequest"] = "AsignacionCompaniaRequest",
                     ["ContextoOperativo"] = "ContextoOperativoDto",
                     ["ContextoOperativoRequest"] = "ContextoOperativoRequest",
                     ["AsignacionUnidadOrganizativa"] = "AsignacionUnidadOrganizativaDto",
                     ["AsignacionUnidadOrganizativaRequest"] = "AsignacionUnidadOrganizativaRequest",
                     ["AsignacionTipoPersona"] = "AsignacionTipoPersonaDto",
                     ["EstadoEfectivoPersona"] = "EstadoEfectivoPersonaDto",
                     ["ContextoOperativoVigente"] = "ContextoOperativoVigenteDto",
                 })
        {
            fixture.DocumentoApi.TieneEsquema(enApi).Should().BeTrue(
                "el contrato declara {0}, que la API serializa como {1}", enContrato, enApi);

            fixture.DocumentoApi.PropiedadesDeEsquema(enApi)
                .Should().BeEquivalentTo(
                    Contrato.PropiedadesPorEsquema[enContrato],
                    porque => porque.WithoutStrictOrdering(),
                    "el esquema {0} debe tener la misma forma que {1}", enContrato, enApi);
        }
    }

    [Theory]
    [InlineData("AsignacionCompaniaRequest")]
    [InlineData("ContextoOperativoRequest")]
    [InlineData("AsignacionUnidadOrganizativaRequest")]
    [InlineData("AsignacionTipoPersonaRequest")]
    public void La_fecha_de_fin_es_obligatoria_en_toda_asociacion_de_persona(string esquema)
    {
        // RF-071: no existe vigencia indefinida en las asociaciones vinculadas a una persona — ni
        // con null ni con fecha centinela. Si alguna volviera a admitirla, esto lo detecta.
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(esquema)
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().Contain("fechaHoraFin");
    }

    [Theory]
    [InlineData("AsignacionCompaniaDto")]
    [InlineData("ContextoOperativoDto")]
    [InlineData("AsignacionUnidadOrganizativaDto")]
    [InlineData("AsignacionTipoPersonaDto")]
    public void La_fecha_de_fin_no_admite_null_en_ninguna_respuesta(string esquema)
    {
        var fin = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(esquema)
            .GetProperty("properties").GetProperty("fechaHoraFin");

        // Un tipo simple "string" (no la unión ["null","string"]) confirma que no es anulable.
        fin.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void El_estado_de_la_pertenencia_expone_los_valores_del_contrato()
    {
        LeerEnum("EstadoPertenencia").Should().BeEquivalentTo(["ACTIVA", "FINALIZADA"]);
    }

    [Fact]
    public void Los_motivos_de_fin_exponen_los_valores_del_contrato()
    {
        LeerEnum("MotivoFinPertenencia")
            .Should().BeEquivalentTo(["CESE_PERTENENCIA", "REEMPLAZO_ASIGNACION"]);

        LeerEnum("MotivoFinRevocacion").Should().BeEquivalentTo(
            ["REVOCACION_CESE_PERTENENCIA", "REEMPLAZO_ASIGNACION", "CIERRE_MANUAL"]);
    }

    [Theory]
    [InlineData("ContextoOperativoDto")]
    [InlineData("AsignacionUnidadOrganizativaDto")]
    public void Las_asociaciones_revocables_exponen_el_origen_de_la_revocacion(string esquema)
    {
        // RF-061/research.md §14.2: responde "qué revocó esta pertenencia" sin heurísticas.
        fixture.DocumentoApi.PropiedadesDeEsquema(esquema)
            .Should().Contain(["estado", "motivoFin", "revocadoPorPertenenciaId"]);
    }

    [Fact]
    public void El_perfil_no_expone_campos_de_revocacion_en_cascada()
    {
        // RF-011/RF-072: AsignaciónTipoPersona no depende de la pertenencia, así que no participa en
        // la cascada. Exponer esos campos sugeriría una dependencia que el dominio no establece.
        var propiedades = fixture.DocumentoApi.PropiedadesDeEsquema("AsignacionTipoPersonaDto");

        propiedades.Should().NotContain("revocadoPorPertenenciaId");
        propiedades.Should().NotContain("motivoFin");
    }

    [Fact]
    public void El_estado_efectivo_devuelve_todos_los_contextos_vigentes()
    {
        // RF-052/CS-013: una persona puede operar con varias Principales a la vez, así que el campo
        // es una colección y no un único contexto.
        var contextos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("EstadoEfectivoPersonaDto")
            .GetProperty("properties").GetProperty("contextosOperativosVigentes");

        contextos.GetProperty("type").GetString().Should().Be("array");
    }

    [Fact]
    public void El_estado_efectivo_exige_la_fecha_a_evaluar()
    {
        var parametro = fixture.DocumentoApi
            .Operacion("/api/personas/{id}/estado-efectivo", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "fechaHora");

        // Sin fecha no hay estado que reconstruir: RF-037 es una consulta a un instante concreto.
        parametro.GetProperty("required").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Valores declarados por un enumerado, descartando el marcador de nulabilidad.
    /// </summary>
    /// <remarks>
    /// Un enumerado anulable se publica con un <c>null</c> adicional dentro de su lista de valores:
    /// eso expresa que el campo admite ausencia, no un valor de negocio más. Compararlo como si lo
    /// fuera haría fallar la prueba sin que nada esté mal.
    /// </remarks>
    private List<string> LeerEnum(string esquema) =>
        [.. fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(esquema)
            .GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString())
            .Where(v => v is not null)
            .Select(v => v!)];
}
