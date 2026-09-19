using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad entre <c>contracts/companies.yaml</c> y lo que la API publica (Historia 2, RF-006,
/// RF-042, RF-051).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class CompaniasContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("companies.yaml");

    private static readonly Dictionary<string, string> EsquemasEquivalentes = new(StringComparer.Ordinal)
    {
        ["Compania"] = "CompaniaDto",
        ["CompaniaRequest"] = "CompaniaRequest",
        ["PaginaCompanias"] = "PaginaResponseOfCompaniaDto",
        ["RelacionContratistaPrincipal"] = "RelacionContratistaPrincipalDto",
        ["RelacionContratistaPrincipalRequest"] = "RelacionContratistaPrincipalRequest",
    };

    [Fact]
    public void Toda_operacion_del_contrato_existe_en_la_api()
    {
        var faltantes = Contrato.Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty(
            "el contrato promete estas operaciones y la API debe exponerlas");
    }

    [Fact]
    public void La_api_no_expone_operaciones_de_companias_fuera_del_contrato()
    {
        var enLaApi = fixture.DocumentoApi.Paths()
            .EnumerateObject()
            .Where(p => p.Name.StartsWith("/api/companias", StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Select(m => (Ruta: p.Name, Metodo: m.Name)))
            .ToList();

        enLaApi.Should().BeEquivalentTo(Contrato.Operaciones);
    }

    [Theory]
    [InlineData("/api/companias", "get")]
    [InlineData("/api/companias", "post")]
    [InlineData("/api/companias/{id}", "get")]
    [InlineData("/api/companias/{id}", "put")]
    [InlineData("/api/companias/{contratistaId}/relaciones-principales", "get")]
    [InlineData("/api/companias/{contratistaId}/relaciones-principales", "post")]
    [InlineData("/api/companias/{contratistaId}/relaciones-principales/{id}/finalizar", "post")]
    public void La_api_declara_todos_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void Los_esquemas_del_contrato_coinciden_en_sus_propiedades_con_los_de_la_api()
    {
        foreach (var (enContrato, enApi) in EsquemasEquivalentes)
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

    [Fact]
    public void El_enumerado_TipoCompania_expone_exactamente_los_valores_del_contrato()
    {
        // RF-042: la clasificación es obligatoria y binaria. Los valores viajan como literales de
        // texto, idénticos a los persistidos en nvarchar.
        var enApi = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("TipoCompania")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        enApi.Should().BeEquivalentTo(["PRINCIPAL_MANDANTE", "CONTRATISTA"]);
    }

    [Fact]
    public void El_listado_acepta_el_filtro_por_tipo_de_compania()
    {
        var parametros = fixture.DocumentoApi.Operacion("/api/companias", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        parametros.Should().Contain(["pagina", "tamañoPagina", "estado", "tipoCompania", "texto"]);
    }

    [Fact]
    public void La_fecha_de_fin_de_la_relacion_admite_null()
    {
        // RF-071 (fecha de fin obligatoria) aplica a las asociaciones vinculadas a una Persona.
        // Ésta une dos compañías, así que null conserva su significado de vigencia abierta: si
        // alguien la volviera no anulable por arrastre, esta prueba lo detecta.
        //
        // Se comprueba el tipo y no la lista "required": en JSON Schema, required significa que la
        // clave esté presente, y "fechaHoraFin": null la satisface. Lo que importa aquí es que null
        // sea un valor admisible.
        var tipos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("RelacionContratistaPrincipalDto")
            .GetProperty("properties").GetProperty("fechaHoraFin")
            .GetProperty("type")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        tipos.Should().Contain("null");
    }

    [Fact]
    public void La_fecha_de_inicio_de_la_relacion_no_admite_null()
    {
        // Contrapunto del caso anterior: una relación siempre nace con inicio conocido.
        var inicio = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("RelacionContratistaPrincipalDto")
            .GetProperty("properties").GetProperty("fechaHoraInicio");

        inicio.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void La_peticion_de_relacion_no_acepta_fecha_de_fin()
    {
        // El contrato solo admite companiaPrincipalId y fechaHoraInicio: una relación se abre
        // vigente y se cierra con /finalizar, no fijando su fin en el alta.
        fixture.DocumentoApi.PropiedadesDeEsquema("RelacionContratistaPrincipalRequest")
            .Should().BeEquivalentTo(["companiaPrincipalId", "fechaHoraInicio"]);
    }

    [Fact]
    public void Todas_las_operaciones_de_companias_requieren_autenticacion()
    {
        foreach (var (ruta, metodo) in Contrato.Operaciones)
        {
            var operacion = fixture.DocumentoApi.Operacion(ruta, metodo);

            if (operacion.TryGetProperty("security", out var seguridad))
            {
                seguridad.GetArrayLength().Should().BeGreaterThan(
                    0, "{0} {1} no debe quedar anónima", metodo, ruta);
            }
        }
    }
}
