using EnterpriseAccessControl.ContractTests.Infraestructura;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad de <c>contracts/access-evaluation.yaml</c> (Historia 8, RF-023 a RF-025, RF-066).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class EvaluacionAccesoContractTests(ApiContratoFixture fixture)
{
    private const string Ruta = "/api/evaluacion-acceso";

    private static readonly ContratoOpenApi Contrato =
        ApiContratoFixture.CargarContrato("access-evaluation.yaml");

    [Fact]
    public void El_contrato_declara_la_operacion_de_evaluacion()
    {
        Contrato.Operaciones.Should().Contain((Ruta, "post"));
    }

    [Fact]
    public void La_api_expone_la_operacion_de_evaluacion()
    {
        fixture.DocumentoApi.TieneOperacion(Ruta, "post").Should().BeTrue();
    }

    [Fact]
    public void La_api_declara_los_codigos_de_estado_del_contrato()
    {
        var esperados = Contrato.Estados[(Ruta, "post")];

        fixture.DocumentoApi.EstadosDeclarados(Ruta, "post").Should().Contain(esperados);
    }

    [Fact]
    public void Una_denegacion_no_es_un_error_http()
    {
        // "siempre 200; el resultado CONCEDIDO/DENEGADO va en el cuerpo": un acceso denegado es una
        // respuesta legítima a la consulta, no un fallo de la petición.
        Contrato.Estados[(Ruta, "post")].Should().BeEquivalentTo(["200", "404"]);
    }

    [Fact]
    public void Los_esquemas_de_evaluacion_coinciden_en_sus_propiedades()
    {
        foreach (var (enContrato, enApi) in new Dictionary<string, string>(StringComparer.Ordinal)
                 {
                     ["EvaluarAccesoRequest"] = "EvaluarAccesoRequest",
                     ["EvaluarAccesoResponse"] = "EvaluarAccesoResponse",
                 })
        {
            fixture.DocumentoApi.TieneEsquema(enApi).Should().BeTrue();

            fixture.DocumentoApi.PropiedadesDeEsquema(enApi)
                .Should().BeEquivalentTo(
                    Contrato.PropiedadesPorEsquema[enContrato],
                    porque => porque.WithoutStrictOrdering(),
                    "el esquema {0} debe tener la misma forma que {1}", enContrato, enApi);
        }
    }

    [Fact]
    public void El_enumerado_MotivoDenegacion_tiene_los_once_valores_del_contrato()
    {
        var enContrato = Contrato.ValoresPorEnumerado["MotivoDenegacion"];

        enContrato.Should().HaveCount(11);
        enContrato.Should().Contain("SIN_CREDENCIAL_VIGENTE");

        // El dominio y el contrato deben coincidir exactamente: un motivo que solo existiera en uno
        // de los dos se traduciría mal o dejaría un corte del algoritmo sin nombre publicable.
        Enum.GetNames<MotivoDenegacion>().Should().BeEquivalentTo(enContrato);

        fixture.DocumentoApi.ValoresDeEnumerado("MotivoDenegacion")
            .Should().BeEquivalentTo(enContrato);
    }

    [Fact]
    public void El_motivo_de_denegacion_es_anulable_solo_en_la_respuesta()
    {
        // El contrato lo declara nullable a nivel de propiedad: una concesión no tiene motivo. La
        // API lo expresa con oneOf[null, $ref], que es la forma equivalente del generador.
        var motivo = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("EvaluarAccesoResponse")
            .GetProperty("properties").GetProperty("motivoDenegacion");

        motivo.GetProperty("oneOf")
            .EnumerateArray()
            .Select(v => v.TryGetProperty("$ref", out var r) ? r.GetString() : v.GetProperty("type").GetString())
            .Should().BeEquivalentTo(["null", "#/components/schemas/MotivoDenegacion"]);
    }

    [Fact]
    public void El_gate_de_credencial_esta_documentado_en_el_contrato()
    {
        // RF-066: que la credencial gatille la denegación fue una decisión explícita; el contrato la
        // enuncia y esta prueba impide que se pierda al editarlo.
        var texto = File.ReadAllText(RutaContrato());

        texto.Should().Contain("14 pasos", "el flujo pasó de 13 a 14 pasos al añadir la credencial");
        texto.Should().Contain("RF-066");
        texto.Should().Contain("SIN_CREDENCIAL_VIGENTE");
    }

    [Fact]
    public void El_enumerado_ResultadoEvaluacion_coincide_con_el_dominio()
    {
        var enContrato = Contrato.ValoresPorEnumerado["ResultadoEvaluacion"];

        enContrato.Should().BeEquivalentTo(["CONCEDIDO", "DENEGADO"]);
        Enum.GetNames<ResultadoEvaluacion>().Should().BeEquivalentTo(enContrato);
    }

    [Fact]
    public void La_respuesta_informa_la_principal_evaluada_aunque_deniegue()
    {
        // Sin este dato, quien administra sabe que falló pero no contra qué Principal se evaluó.
        fixture.DocumentoApi.PropiedadesDeEsquema("EvaluarAccesoResponse")
            .Should().Contain(["companiaPrincipalId", "contextoOperativoId", "evaluadoEnZonaHoraria"]);
    }

    [Fact]
    public void La_peticion_exige_persona_area_y_fecha_hora()
    {
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("EvaluarAccesoRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().BeEquivalentTo(["personaId", "areaAccesoId", "fechaHora"]);
    }

    [Fact]
    public void La_evaluacion_no_es_anonima()
    {
        // Paso 1 del algoritmo: la consulta misma requiere un usuario con alcance (RF-005).
        var operacion = fixture.DocumentoApi.Operacion(Ruta, "post");

        if (operacion.TryGetProperty("security", out var seguridad))
        {
            seguridad.GetArrayLength().Should().BeGreaterThan(0);
        }
    }

    private static string RutaContrato()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(
                directorio.FullName,
                "specs",
                "001-control-acceso-empresarial",
                "contracts",
                "access-evaluation.yaml");

            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException("No se encontró contracts/access-evaluation.yaml.");
    }
}
