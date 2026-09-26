using EnterpriseAccessControl.ContractTests.Infraestructura;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad de <c>contracts/permissions.yaml</c> (Historia 8, RF-020 a RF-022, RF-039, RF-071).
/// </summary>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class PermisosContractTests(ApiContratoFixture fixture)
{
    private static readonly ContratoOpenApi Contrato =
        ApiContratoFixture.CargarContrato("permissions.yaml");

    private static readonly (string Ruta, string Metodo)[] Operaciones =
    [
        ("/api/permisos", "get"),
        ("/api/permisos", "post"),
        ("/api/permisos/{id}", "get"),
        ("/api/permisos/{id}", "put"),
    ];

    [Fact]
    public void El_contrato_declara_las_operaciones_de_permisos()
    {
        Contrato.Operaciones.Should().Contain(Operaciones);
    }

    [Fact]
    public void Todas_las_operaciones_existen_en_la_api()
    {
        var faltantes = Operaciones
            .Where(op => !fixture.DocumentoApi.TieneOperacion(op.Ruta, op.Metodo))
            .ToList();

        faltantes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/permisos", "get")]
    [InlineData("/api/permisos", "post")]
    [InlineData("/api/permisos/{id}", "get")]
    [InlineData("/api/permisos/{id}", "put")]
    public void La_api_declara_los_codigos_de_estado_del_contrato(string ruta, string metodo)
    {
        var esperados = Contrato.Estados[(ruta, metodo)];
        var declarados = fixture.DocumentoApi.EstadosDeclarados(ruta, metodo);

        declarados.Should().Contain(esperados);
    }

    [Fact]
    public void Los_esquemas_de_permiso_coinciden_en_sus_propiedades()
    {
        foreach (var (enContrato, enApi) in new Dictionary<string, string>(StringComparer.Ordinal)
                 {
                     ["PermisoAcceso"] = "PermisoAccesoDto",
                     ["PermisoAccesoRequest"] = "PermisoAccesoRequest",
                     ["PaginaPermisos"] = "PaginaResponseOfPermisoAccesoDto",
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
    public void La_vigencia_completa_es_obligatoria_para_los_tres_alcances()
    {
        // RF-021/RF-071: el fin de vigencia no es opcional ni admite centinela, y la obligación no
        // depende del alcance. Es justo el punto que la documentación previa tenía mal.
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("PermisoAccesoRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        // v2.0.0 (VF-004, RF-083): la vigencia de la petición son dos fechas civiles.
        requeridos.Should().Contain(["fechaInicioVigencia", "fechaFinVigencia"]);
        requeridos.Should().BeEquivalentTo(Contrato.PropiedadesPorEsquema["PermisoAccesoRequest"]
            .Where(p => p is "areaAccesoId" or "alcance" or "fechaInicioVigencia"
                or "fechaFinVigencia" or "estado" or "bloquesHorarios"));
    }

    [Fact]
    public void La_peticion_v2_recibe_la_vigencia_como_fechas_y_no_como_instantes()
    {
        // F-3: permissions.yaml v2.0.0 declara fechaInicioVigencia/fechaFinVigencia con format: date y retira
        // los campos date-time de la petición. La API publica exactamente esa forma.
        Contrato.PropiedadesPorEsquema["PermisoAccesoRequest"]
            .Should().Contain(["fechaInicioVigencia", "fechaFinVigencia"])
            .And.NotContain(["fechaHoraInicioVigencia", "fechaHoraFinVigencia"]);

        var propiedades = PropiedadesEnApi("PermisoAccesoRequest");

        Formato(propiedades, "fechaInicioVigencia").Should().Be("date");
        Formato(propiedades, "fechaFinVigencia").Should().Be("date");
        propiedades.TryGetProperty("fechaHoraInicioVigencia", out _).Should().BeFalse();
        propiedades.TryGetProperty("fechaHoraFinVigencia", out _).Should().BeFalse();
    }

    [Fact]
    public void La_respuesta_v2_expone_los_instantes_efectivos_las_fechas_civiles_y_su_zona()
    {
        // research.md §36.5: instantes UTC efectivos (date-time), fechas civiles (date), si la vigencia es de
        // días completos y la zona efectiva con que se calcularon.
        Contrato.PropiedadesPorEsquema["PermisoAcceso"].Should().Contain(
        [
            "fechaHoraInicioVigencia", "fechaHoraFinVigencia", "fechaInicioVigencia", "fechaFinVigencia",
            "vigenciaEnDiasCompletos", "zonaHorariaIana",
        ]);

        var propiedades = PropiedadesEnApi("PermisoAccesoDto");

        Formato(propiedades, "fechaHoraInicioVigencia").Should().Be("date-time");
        Formato(propiedades, "fechaHoraFinVigencia").Should().Be("date-time");
        Formato(propiedades, "fechaInicioVigencia").Should().Be("date");
        Formato(propiedades, "fechaFinVigencia").Should().Be("date");
        propiedades.GetProperty("vigenciaEnDiasCompletos").GetProperty("type").GetString().Should().Be("boolean");
        propiedades.GetProperty("zonaHorariaIana").GetProperty("type").GetString().Should().Be("string");
    }

    private System.Text.Json.JsonElement PropiedadesEnApi(string esquema) =>
        fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(esquema)
            .GetProperty("properties");

    private static string? Formato(System.Text.Json.JsonElement propiedades, string nombre) =>
        propiedades.GetProperty(nombre).GetProperty("format").GetString();

    [Fact]
    public void El_fin_de_vigencia_no_es_anulable()
    {
        // Si el esquema lo declarase nullable, un cliente podría enviar null y reintroducir la
        // "vigencia abierta" que RF-071 eliminó.
        var propiedad = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("PermisoAccesoDto")
            .GetProperty("properties").GetProperty("fechaHoraFinVigencia");

        propiedad.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void Los_tres_sujetos_son_opcionales_porque_solo_uno_aplica()
    {
        // JSON Schema no puede expresar "exactamente uno según el alcance": los tres van opcionales
        // y la exclusividad la imponen el servicio y un CHECK en la base de datos.
        var requeridos = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("PermisoAccesoRequest")
            .GetProperty("required")
            .EnumerateArray()
            .Select(v => v.GetString())
            .ToList();

        requeridos.Should().NotContain(["personaId", "unidadOrganizativaId", "companiaId"]);
    }

    [Fact]
    public void El_enumerado_AlcancePermiso_coincide_con_el_contrato_y_con_el_dominio()
    {
        var enContrato = Contrato.ValoresPorEnumerado["AlcancePermiso"];

        enContrato.Should().BeEquivalentTo(["PERSONA", "UNIDAD_ORGANIZATIVA", "COMPANIA"]);
        Enum.GetNames<AlcancePermiso>().Should().BeEquivalentTo(enContrato);

        fixture.DocumentoApi.ValoresDeEnumerado("AlcancePermiso")
            .Should().BeEquivalentTo(enContrato);
    }

    [Fact]
    public void El_alcance_del_permiso_no_es_anulable()
    {
        // El esquema compartido de AlcancePermiso lleva null en su lista de valores porque
        // nivelAplicado (evaluación) lo usa como anulable. Lo que no debe ocurrir es que la
        // propiedad alcance de un permiso lo sea: un permiso sin alcance es inevaluable.
        var alcance = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("PermisoAccesoDto")
            .GetProperty("properties").GetProperty("alcance");

        alcance.TryGetProperty("oneOf", out _).Should().BeFalse();
        alcance.GetProperty("$ref").GetString().Should().EndWith("AlcancePermiso");
    }

    [Fact]
    public void El_enumerado_DiaSemana_coincide_con_el_contrato_y_con_el_dominio()
    {
        var enContrato = Contrato.ValoresPorEnumerado["DiaSemana"];

        enContrato.Should().HaveCount(7);
        Enum.GetNames<DiaSemana>().Should().BeEquivalentTo(enContrato);

        fixture.DocumentoApi.ValoresDeEnumerado("DiaSemana").Should().BeEquivalentTo(enContrato);
    }

    [Fact]
    public void El_bloque_horario_viaja_como_hora_local_y_no_como_instante()
    {
        // RF-022: un bloque es una regla de negocio en hora local ("lunes de 08:00 a 17:00").
        // Serializarlo como date-time obligaría a inventar una fecha y perdería el sentido.
        var propiedades = fixture.DocumentoApi.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("BloqueHorarioDto")
            .GetProperty("properties");

        foreach (var campo in new[] { "horaInicio", "horaFin" })
        {
            propiedades.GetProperty(campo).GetProperty("type").GetString().Should().Be("string");

            propiedades.GetProperty(campo).TryGetProperty("format", out var formato)
                .Should().BeFalse("{0} no es un instante; recibido formato '{1}'", campo, formato);
        }
    }

    [Theory]
    [InlineData("/api/permisos", "post")]
    [InlineData("/api/permisos/{id}", "put")]
    public void Las_escrituras_declaran_los_rechazos_de_contencion_y_alcance_de_RF_082(string ruta, string metodo)
    {
        // Cambio post-Baseline VF-007 (permissions.yaml v1.1.0): con alcance PERSONA, 400
        // SIN_PERTENENCIA_VIGENTE, 404 por persona fuera del alcance (evaluado antes de la pertenencia,
        // research.md §35.6) y 409 FUERA_DE_CONTENCION_TEMPORAL, en el contrato y en la API.
        Contrato.Estados[(ruta, metodo)].Should().Contain(["400", "404", "409"]);
        fixture.DocumentoApi.EstadosDeclarados(ruta, metodo).Should().Contain(["400", "404", "409"]);
    }

    [Fact]
    public void Ninguna_operacion_de_permisos_es_anonima()
    {
        foreach (var (ruta, metodo) in Operaciones)
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
