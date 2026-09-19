using System.Globalization;
using System.Text.Json;
using EnterpriseAccessControl.ContractTests.Infraestructura;
using FluentAssertions;
using YamlDotNet.Serialization;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Ningún cuerpo de petición expone los campos de auditoría como editables
/// (Historia 10; RF-027, CS-005; research.md §6).
/// </summary>
/// <remarks>
/// La auditoría la genera el sistema. Si un esquema de entrada declarase <c>createdAt</c> o
/// <c>updatedById</c>, el contrato estaría invitando a los clientes a enviarlos, aunque el servidor los
/// ignore. Se comprueba en los dos lados: los contratos YAML y el documento OpenAPI que publica la API.
///
/// No basta con mirar los esquemas llamados <c>*Request</c>: varios contratos definen el cuerpo en línea
/// dentro de la operación (p. ej. <c>/mover</c> o <c>tipos-persona</c>), así que también se recorren los
/// <c>requestBody</c> de todas las operaciones.
/// </remarks>
[Collection(ApiContratoFixtureDefinition.Name)]
public sealed class AuditFieldsNotExposedTests(ApiContratoFixture fixture)
{
    private static readonly string[] CamposDeAuditoria =
        ["createdAt", "updatedAt", "createdById", "updatedById"];

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
    public void Ningun_esquema_Request_del_contrato_declara_campos_de_auditoria(string contrato)
    {
        var raiz = CargarYaml(contrato);

        var expuestos = EsquemasDeEntrada(raiz)
            .SelectMany(e => PropiedadesYaml(e.Esquema, raiz).Select(p => $"{e.Origen}.{p}"))
            .Where(p => CamposDeAuditoria.Any(c => p.EndsWith($".{c}", StringComparison.Ordinal)))
            .ToList();

        expuestos.Should().BeEmpty("{0} no debe aceptar campos de auditoría en la entrada", contrato);
    }

    [Fact]
    public void Los_contratos_tienen_esquemas_de_entrada_que_revisar()
    {
        // Evita que la prueba anterior pase vacía por un cambio de estructura de los YAML.
        var total = Directory.GetFiles(RaizContratos(), "*.yaml")
            .Sum(ruta => EsquemasDeEntrada(CargarYaml(Path.GetFileName(ruta))).Count());

        total.Should().BeGreaterThan(20);
    }

    [Fact]
    public void Ningun_cuerpo_de_peticion_de_la_api_publicada_declara_campos_de_auditoria()
    {
        var documento = fixture.DocumentoApi.RootElement;
        var esquemas = documento.GetProperty("components").GetProperty("schemas");

        var revisados = 0;
        var expuestos = new List<string>();

        foreach (var ruta in documento.GetProperty("paths").EnumerateObject())
        {
            foreach (var operacion in ruta.Value.EnumerateObject())
            {
                if (!operacion.Value.TryGetProperty("requestBody", out var cuerpo) ||
                    !cuerpo.TryGetProperty("content", out var contenido))
                {
                    continue;
                }

                foreach (var tipo in contenido.EnumerateObject())
                {
                    if (!tipo.Value.TryGetProperty("schema", out var esquema))
                    {
                        continue;
                    }

                    revisados++;

                    foreach (var propiedad in PropiedadesJson(esquema, esquemas))
                    {
                        if (CamposDeAuditoria.Contains(propiedad, StringComparer.OrdinalIgnoreCase))
                        {
                            expuestos.Add($"{operacion.Name.ToUpperInvariant()} {ruta.Name}: {propiedad}");
                        }
                    }
                }
            }
        }

        revisados.Should().BeGreaterThan(20, "premisa: la API publica cuerpos de petición que revisar");
        expuestos.Should().BeEmpty();
    }

    // --- Lectura de los contratos YAML ----------------------------------------------------------------

    private static IEnumerable<(string Origen, Dictionary<object, object> Esquema)> EsquemasDeEntrada(
        Dictionary<object, object> raiz)
    {
        // 1) Esquemas con nombre de entrada.
        if (raiz.TryGetValue("components", out var c) && c is Dictionary<object, object> componentes &&
            componentes.TryGetValue("schemas", out var s) && s is Dictionary<object, object> esquemas)
        {
            foreach (var (nombre, definicion) in esquemas)
            {
                var texto = Convert.ToString(nombre, CultureInfo.InvariantCulture)!;

                if (texto.EndsWith("Request", StringComparison.Ordinal) &&
                    definicion is Dictionary<object, object> esquema)
                {
                    yield return (texto, esquema);
                }
            }
        }

        // 2) Cuerpos de petición de cada operación, con nombre o en línea.
        if (raiz.TryGetValue("paths", out var p) && p is Dictionary<object, object> rutas)
        {
            foreach (var (ruta, item) in rutas)
            {
                if (item is not Dictionary<object, object> operaciones)
                {
                    continue;
                }

                foreach (var (metodo, operacion) in operaciones)
                {
                    if (operacion is Dictionary<object, object> detalle &&
                        detalle.TryGetValue("requestBody", out var rb) && rb is Dictionary<object, object> cuerpo &&
                        cuerpo.TryGetValue("content", out var ct) && ct is Dictionary<object, object> contenido)
                    {
                        foreach (var (_, medio) in contenido)
                        {
                            if (medio is Dictionary<object, object> m &&
                                m.TryGetValue("schema", out var sc) && sc is Dictionary<object, object> esquema)
                            {
                                yield return ($"{metodo} {ruta}", esquema);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>Propiedades de un esquema YAML, resolviendo <c>$ref</c> y <c>allOf</c>.</summary>
    private static IEnumerable<string> PropiedadesYaml(Dictionary<object, object> esquema, Dictionary<object, object> raiz)
    {
        if (esquema.TryGetValue("$ref", out var referencia))
        {
            var nombre = Convert.ToString(referencia, CultureInfo.InvariantCulture)!.Split('/')[^1];

            if (raiz["components"] is Dictionary<object, object> componentes &&
                componentes["schemas"] is Dictionary<object, object> esquemas &&
                esquemas.TryGetValue(nombre, out var destino) &&
                destino is Dictionary<object, object> resuelto)
            {
                foreach (var propiedad in PropiedadesYaml(resuelto, raiz))
                {
                    yield return propiedad;
                }
            }

            yield break;
        }

        if (esquema.TryGetValue("properties", out var props) && props is Dictionary<object, object> propiedades)
        {
            foreach (var (nombre, _) in propiedades)
            {
                yield return Convert.ToString(nombre, CultureInfo.InvariantCulture)!;
            }
        }

        if (esquema.TryGetValue("allOf", out var todos) && todos is List<object> partes)
        {
            foreach (var parte in partes.OfType<Dictionary<object, object>>())
            {
                foreach (var propiedad in PropiedadesYaml(parte, raiz))
                {
                    yield return propiedad;
                }
            }
        }

        if (esquema.TryGetValue("items", out var items) && items is Dictionary<object, object> elemento)
        {
            foreach (var propiedad in PropiedadesYaml(elemento, raiz))
            {
                yield return propiedad;
            }
        }
    }

    /// <summary>Propiedades de un esquema del documento publicado, resolviendo <c>$ref</c>.</summary>
    private static IEnumerable<string> PropiedadesJson(JsonElement esquema, JsonElement esquemas)
    {
        if (esquema.TryGetProperty("$ref", out var referencia))
        {
            var nombre = referencia.GetString()!.Split('/')[^1];

            if (esquemas.TryGetProperty(nombre, out var resuelto))
            {
                foreach (var propiedad in PropiedadesJson(resuelto, esquemas))
                {
                    yield return propiedad;
                }
            }

            yield break;
        }

        if (esquema.TryGetProperty("properties", out var propiedades))
        {
            foreach (var propiedad in propiedades.EnumerateObject())
            {
                yield return propiedad.Name;
            }
        }

        if (esquema.TryGetProperty("items", out var items))
        {
            foreach (var propiedad in PropiedadesJson(items, esquemas))
            {
                yield return propiedad;
            }
        }
    }

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
