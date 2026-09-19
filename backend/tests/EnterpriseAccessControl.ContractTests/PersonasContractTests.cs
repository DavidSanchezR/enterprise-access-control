using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad entre los endpoints de Persona de <c>contracts/people.yaml</c> y lo que la API publica
/// (Historia 4, RF-012, RF-013, RF-041).
/// </summary>
/// <remarks>
/// <c>people.yaml</c> cubre también el histórico de pertenencia y los contextos operativos, que
/// pertenecen a la Historia 5: esta prueba se limita a las cuatro operaciones de Persona y no exige
/// aún el resto, para que la conformidad de US4 pueda verificarse sin implementar US5.
/// </remarks>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class PersonasContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato = ApiContratoFixture.CargarContrato("people.yaml");

    /// <summary>Operaciones de Persona propiamente dicha (Historia 4).</summary>
    private static readonly (string Ruta, string Metodo)[] OperacionesUs4 =
    [
        ("/api/personas", "get"),
        ("/api/personas", "post"),
        ("/api/personas/{id}", "get"),
        ("/api/personas/{id}", "put"),
    ];

    [Fact]
    public void El_contrato_declara_las_cuatro_operaciones_de_persona()
    {
        Contrato.Operaciones.Should().Contain(OperacionesUs4);
    }

    [Fact]
    public void Las_cuatro_operaciones_de_persona_existen_en_la_api()
    {
        var faltantes = OperacionesUs4
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/personas", "get")]
    [InlineData("/api/personas", "post")]
    [InlineData("/api/personas/{id}", "get")]
    [InlineData("/api/personas/{id}", "put")]
    public void La_api_declara_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void Los_esquemas_de_persona_coinciden_en_sus_propiedades()
    {
        foreach (var (enContrato, enApi) in new Dictionary<string, string>(StringComparer.Ordinal)
                 {
                     ["Persona"] = "PersonaDto",
                     ["PersonaRequest"] = "PersonaRequest",
                     ["PaginaPersonas"] = "PaginaResponseOfPersonaDto",
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

    [Fact]
    public void La_peticion_exige_los_diez_campos_obligatorios()
    {
        // RF-012: el registro no admite datos parciales. Si alguno dejara de ser obligatorio, se
        // podrían dar de alta personas sin contacto de emergencia ni tipo de sangre.
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("PersonaRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().BeEquivalentTo(
        [
            "nombres", "apellidos", "fechaNacimiento", "tipoDocumentoId", "numeroDocumento",
            "generoId", "correoElectronico", "tipoSangreId", "contactoEmergencia", "numeroEmergencia",
        ]);
    }

    [Fact]
    public void La_peticion_no_acepta_el_identificador_de_la_persona()
    {
        // RF-013: el Id es autogenerado e inmutable. Aceptarlo en el cuerpo permitiría fijarlo o
        // cambiarlo desde el cliente.
        fixture.DocumentoApi.PropiedadesDeEsquema("PersonaRequest")
            .Should().NotContain("id");

        fixture.DocumentoApi.PropiedadesDeEsquema("PersonaDto")
            .Should().Contain("id", "la respuesta sí lo expone, para poder referenciar el recurso");
    }

    [Fact]
    public void La_fecha_de_nacimiento_viaja_como_fecha_sin_hora()
    {
        // Es un dato civil, no un instante: arrastrar hora induciría desplazamientos por zona
        // horaria al mostrarla.
        var fecha = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("PersonaDto")
            .GetProperty("properties").GetProperty("fechaNacimiento");

        fecha.GetProperty("type").GetString().Should().Be("string");
        fecha.GetProperty("format").GetString().Should().Be("date");
    }

    [Fact]
    public void La_busqueda_acepta_los_filtros_del_contrato()
    {
        var parametros = fixture.DocumentoApi.Operacion("/api/personas", "get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        parametros.Should().Contain(
            ["pagina", "tamañoPagina", "texto", "tipoDocumentoId", "companiaId"]);
    }

    [Fact]
    public void Las_operaciones_de_persona_requieren_autenticacion()
    {
        foreach (var (ruta, metodo) in OperacionesUs4)
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
