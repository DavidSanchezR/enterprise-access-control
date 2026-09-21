using System.Globalization;
using System.Text.Json;
using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;
using YamlDotNet.Serialization;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// El documento OpenAPI que publica la API (<c>/openapi/v1.json</c>, <c>Microsoft.AspNetCore.OpenApi</c>)
/// coincide con el conjunto completo de <c>contracts/*.yaml</c> (research.md §10, RF-040).
/// </summary>
/// <remarks>
/// Las pruebas de contrato de cada historia verifican su grupo funcional con detalle. Esta es la red
/// transversal: recorre <em>todas</em> las operaciones de <em>todos</em> los contratos y compara, por
/// operación, lo que un cliente observa — que la ruta exista, los códigos de estado, y la forma de la
/// petición y de la respuesta de éxito —, más los valores de cada enumerado que ambos documentos
/// declaran con el mismo nombre.
///
/// Se compara por operación y no por nombre de esquema porque los nombres difieren legítimamente entre
/// ambos lados (<c>AreaAcceso</c> en el contrato, <c>AreaAccesoDto</c> en C#): lo que importa al cliente
/// es la forma del JSON en cada endpoint, no cómo se llama el tipo que lo produce.
///
/// Se comparan las propiedades de primer nivel (y las de los elementos cuando la respuesta es una
/// lista). Un objeto anidado —p. ej. los <c>items</c> de una página— se verifica en las operaciones
/// donde ese mismo esquema es la respuesta de primer nivel.
/// </remarks>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class OpenApiSnapshotTests(ApiContratoFixture fixture)
{
    private static readonly string[] MetodosHttp = ["get", "post", "put", "patch", "delete"];

    public static TheoryData<string> Contratos()
    {
        var datos = new TheoryData<string>();

        foreach (var ruta in Directory.GetFiles(RaizContratos(), "*.yaml").Order(StringComparer.Ordinal))
        {
            datos.Add(Path.GetFileName(ruta));
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Contratos))]
    public void Cada_operacion_del_contrato_existe_con_sus_codigos_de_estado(string contrato)
    {
        var raiz = CargarYaml(contrato);
        var diferencias = new List<string>();

        foreach (var (ruta, metodo, operacion) in Operaciones(raiz))
        {
            if (!fixture.DocumentoApi.TieneOperacion(ruta, metodo))
            {
                diferencias.Add($"{metodo.ToUpperInvariant()} {ruta}: no existe en la API");
                continue;
            }

            var esperados = operacion.TryGetValue("responses", out var r) && r is Dictionary<object, object> respuestas
                ? respuestas.Keys.Select(k => Convert.ToString(k, CultureInfo.InvariantCulture)!).ToList()
                : [];

            var faltantes = esperados.Except(fixture.DocumentoApi.EstadosDeclarados(ruta, metodo)).ToList();

            if (faltantes.Count > 0)
            {
                diferencias.Add($"{metodo.ToUpperInvariant()} {ruta}: la API no declara {string.Join(", ", faltantes)}");
            }
        }

        diferencias.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Contratos))]
    public void Cada_cuerpo_de_peticion_tiene_la_forma_del_contrato(string contrato)
    {
        var raiz = CargarYaml(contrato);
        var diferencias = new List<string>();

        foreach (var (ruta, metodo, operacion) in Operaciones(raiz))
        {
            var enContrato = EsquemaDeCuerpo(operacion);
            if (enContrato is null || !fixture.DocumentoApi.TieneOperacion(ruta, metodo))
            {
                continue;
            }

            var enApi = fixture.DocumentoApi.Operacion(ruta, metodo)
                .TryGetProperty("requestBody", out var cuerpo)
                ? EsquemaJson(cuerpo)
                : (JsonElement?)null;

            Comparar($"{metodo.ToUpperInvariant()} {ruta} (petición)", enContrato, enApi, raiz, diferencias);
        }

        diferencias.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Contratos))]
    public void Cada_respuesta_de_exito_tiene_la_forma_del_contrato(string contrato)
    {
        var raiz = CargarYaml(contrato);
        var diferencias = new List<string>();

        foreach (var (ruta, metodo, operacion) in Operaciones(raiz))
        {
            if (!fixture.DocumentoApi.TieneOperacion(ruta, metodo) ||
                !operacion.TryGetValue("responses", out var r) || r is not Dictionary<object, object> respuestas)
            {
                continue;
            }

            foreach (var (codigo, respuesta) in respuestas)
            {
                var estado = Convert.ToString(codigo, CultureInfo.InvariantCulture)!;

                if (!estado.StartsWith('2') || respuesta is not Dictionary<object, object> detalle)
                {
                    continue;
                }

                var enContrato = EsquemaDeCuerpo(detalle);
                if (enContrato is null)
                {
                    continue;
                }

                var apiRespuestas = fixture.DocumentoApi.Operacion(ruta, metodo).GetProperty("responses");
                var enApi = apiRespuestas.TryGetProperty(estado, out var apiDetalle) ? EsquemaJson(apiDetalle) : null;

                Comparar($"{metodo.ToUpperInvariant()} {ruta} ({estado})", enContrato, enApi, raiz, diferencias);
            }
        }

        diferencias.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Contratos))]
    public void Los_enumerados_con_el_mismo_nombre_tienen_los_mismos_valores(string contrato)
    {
        var raiz = CargarYaml(contrato);
        var esquemasApi = fixture.DocumentoApi.RootElement.GetProperty("components").GetProperty("schemas");
        var diferencias = new List<string>();

        foreach (var (nombre, definicion) in EsquemasYaml(raiz))
        {
            if (!definicion.TryGetValue("enum", out var e) || e is not List<object> valores ||
                !esquemasApi.TryGetProperty(nombre, out _))
            {
                continue;
            }

            var enContrato = valores.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)!).Order(StringComparer.Ordinal);
            var enApi = fixture.DocumentoApi.ValoresDeEnumerado(nombre).Order(StringComparer.Ordinal);

            if (!enContrato.SequenceEqual(enApi))
            {
                diferencias.Add($"{nombre}: contrato [{string.Join(", ", enContrato)}] ≠ API [{string.Join(", ", enApi)}]");
            }
        }

        diferencias.Should().BeEmpty();
    }

    [Fact]
    public void Incluye_el_endpoint_de_renovacion_y_los_doce_motivos_de_denegacion()
    {
        // Mencionados expresamente en T162: son los dos últimos añadidos al contrato y los más fáciles
        // de perder en una regeneración.
        fixture.DocumentoApi.TieneOperacion("/api/personas/{id}/historial-companias/{asignacionId}/renovar", "post")
            .Should().BeTrue();

        fixture.DocumentoApi.ValoresDeEnumerado("MotivoDenegacion").Should().HaveCount(12);
    }

    [Fact]
    public void Se_revisan_todos_los_contratos_y_un_volumen_significativo_de_operaciones()
    {
        // Evita que las teorías anteriores pasen vacías si cambia la estructura de los YAML.
        var archivos = Directory.GetFiles(RaizContratos(), "*.yaml");
        var operaciones = archivos.Sum(a => Operaciones(CargarYaml(Path.GetFileName(a))).Count());

        archivos.Should().HaveCountGreaterThanOrEqualTo(9, "research.md §10 define nueve grupos funcionales");
        operaciones.Should().BeGreaterThan(50);
    }

    // --- Comparación ----------------------------------------------------------------------------------

    private void Comparar(
        string donde,
        Dictionary<object, object> contrato,
        JsonElement? api,
        Dictionary<object, object> raiz,
        List<string> diferencias)
    {
        if (api is null)
        {
            diferencias.Add($"{donde}: la API no declara esquema");
            return;
        }

        var esquemasApi = fixture.DocumentoApi.RootElement.GetProperty("components").GetProperty("schemas");

        var (esArrayContrato, propiedadesContrato) = FormaYaml(contrato, raiz);
        var (esArrayApi, propiedadesApi) = FormaJson(api.Value, esquemasApi);

        if (esArrayContrato != esArrayApi)
        {
            diferencias.Add($"{donde}: el contrato declara {(esArrayContrato ? "una lista" : "un objeto")} y la API no");
            return;
        }

        var soloContrato = propiedadesContrato.Except(propiedadesApi, StringComparer.Ordinal).ToList();
        var soloApi = propiedadesApi.Except(propiedadesContrato, StringComparer.Ordinal).ToList();

        if (soloContrato.Count > 0)
        {
            diferencias.Add($"{donde}: faltan en la API [{string.Join(", ", soloContrato)}]");
        }

        if (soloApi.Count > 0)
        {
            diferencias.Add($"{donde}: la API expone de más [{string.Join(", ", soloApi)}]");
        }
    }

    private static (bool EsArray, HashSet<string> Propiedades) FormaYaml(
        Dictionary<object, object> esquema,
        Dictionary<object, object> raiz)
    {
        esquema = ResolverYaml(esquema, raiz);

        if (Tipo(esquema) == "array" && esquema.TryGetValue("items", out var i) && i is Dictionary<object, object> items)
        {
            return (true, FormaYaml(items, raiz).Propiedades);
        }

        var propiedades = new HashSet<string>(StringComparer.Ordinal);

        if (esquema.TryGetValue("properties", out var p) && p is Dictionary<object, object> mapa)
        {
            foreach (var clave in mapa.Keys)
            {
                propiedades.Add(Convert.ToString(clave, CultureInfo.InvariantCulture)!);
            }
        }

        if (esquema.TryGetValue("allOf", out var a) && a is List<object> partes)
        {
            foreach (var parte in partes.OfType<Dictionary<object, object>>())
            {
                propiedades.UnionWith(FormaYaml(parte, raiz).Propiedades);
            }
        }

        return (false, propiedades);
    }

    private static (bool EsArray, HashSet<string> Propiedades) FormaJson(JsonElement esquema, JsonElement esquemas)
    {
        if (esquema.TryGetProperty("$ref", out var referencia))
        {
            return FormaJson(esquemas.GetProperty(referencia.GetString()!.Split('/')[^1]), esquemas);
        }

        var esArray = esquema.TryGetProperty("type", out var tipo) &&
                      (tipo.ValueKind == JsonValueKind.String
                          ? tipo.GetString() == "array"
                          : tipo.EnumerateArray().Any(t => t.GetString() == "array"));

        if (esArray && esquema.TryGetProperty("items", out var items))
        {
            return (true, FormaJson(items, esquemas).Propiedades);
        }

        var propiedades = new HashSet<string>(StringComparer.Ordinal);

        if (esquema.TryGetProperty("properties", out var mapa))
        {
            foreach (var propiedad in mapa.EnumerateObject())
            {
                propiedades.Add(propiedad.Name);
            }
        }

        return (false, propiedades);
    }

    // --- Lectura --------------------------------------------------------------------------------------

    private static IEnumerable<(string Ruta, string Metodo, Dictionary<object, object> Operacion)> Operaciones(
        Dictionary<object, object> raiz)
    {
        if (!raiz.TryGetValue("paths", out var p) || p is not Dictionary<object, object> rutas)
        {
            yield break;
        }

        foreach (var (ruta, item) in rutas)
        {
            if (item is not Dictionary<object, object> operaciones)
            {
                continue;
            }

            foreach (var (clave, operacion) in operaciones)
            {
                var metodo = Convert.ToString(clave, CultureInfo.InvariantCulture)!;

                if (MetodosHttp.Contains(metodo, StringComparer.Ordinal) && operacion is Dictionary<object, object> detalle)
                {
                    yield return (Convert.ToString(ruta, CultureInfo.InvariantCulture)!, metodo, detalle);
                }
            }
        }
    }

    /// <summary>Esquema JSON de un <c>requestBody</c> o de una respuesta, si declara contenido.</summary>
    private static Dictionary<object, object>? EsquemaDeCuerpo(Dictionary<object, object> contenedor)
    {
        if (contenedor.TryGetValue("requestBody", out var rb) && rb is Dictionary<object, object> cuerpo)
        {
            contenedor = cuerpo;
        }

        return contenedor.TryGetValue("content", out var c) && c is Dictionary<object, object> contenido &&
               contenido.TryGetValue("application/json", out var m) && m is Dictionary<object, object> medio &&
               medio.TryGetValue("schema", out var s) && s is Dictionary<object, object> esquema
            ? esquema
            : null;
    }

    private static JsonElement? EsquemaJson(JsonElement contenedor) =>
        contenedor.TryGetProperty("content", out var contenido) &&
        contenido.TryGetProperty("application/json", out var medio) &&
        medio.TryGetProperty("schema", out var esquema)
            ? esquema
            : null;

    private static Dictionary<object, object> ResolverYaml(Dictionary<object, object> esquema, Dictionary<object, object> raiz)
    {
        while (esquema.TryGetValue("$ref", out var referencia))
        {
            var nombre = Convert.ToString(referencia, CultureInfo.InvariantCulture)!.Split('/')[^1];
            esquema = EsquemasYaml(raiz).Single(e => e.Nombre == nombre).Esquema;
        }

        return esquema;
    }

    private static IEnumerable<(string Nombre, Dictionary<object, object> Esquema)> EsquemasYaml(Dictionary<object, object> raiz)
    {
        if (raiz.TryGetValue("components", out var c) && c is Dictionary<object, object> componentes &&
            componentes.TryGetValue("schemas", out var s) && s is Dictionary<object, object> esquemas)
        {
            foreach (var (nombre, definicion) in esquemas)
            {
                if (definicion is Dictionary<object, object> esquema)
                {
                    yield return (Convert.ToString(nombre, CultureInfo.InvariantCulture)!, esquema);
                }
            }
        }
    }

    private static string? Tipo(Dictionary<object, object> esquema) =>
        esquema.TryGetValue("type", out var t) ? Convert.ToString(t, CultureInfo.InvariantCulture) : null;

    private static Dictionary<object, object> CargarYaml(string contrato) =>
        new DeserializerBuilder().Build()
            .Deserialize<Dictionary<object, object>>(File.ReadAllText(Path.Combine(RaizContratos(), contrato)));

    private static string RaizContratos()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, "specs", "001-control-acceso-empresarial", "contracts");

            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la carpeta de contratos.");
    }
}
