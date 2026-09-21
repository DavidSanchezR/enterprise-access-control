using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;

namespace EnterpriseAccessControl.ContractTests.Infraestructura;

/// <summary>
/// Arranca la API en memoria y expone tanto el documento OpenAPI que genera como los contratos
/// declarados en <c>specs/001-control-acceso-empresarial/contracts/</c>.
/// </summary>
/// <remarks>
/// Estas pruebas son de <em>conformidad estructural</em>: verifican que lo que la API publica coincide
/// con lo que el contrato promete (rutas, métodos, códigos de estado, nombres de propiedades). No
/// necesitan base de datos, así que no arrancan contenedor: el comportamiento en tiempo de ejecución
/// (login real, bloqueo, expiración) se verifica en las pruebas de integración, que sí usan SQL Server.
/// </remarks>
public sealed class ApiContratoFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    /// <summary>Documento OpenAPI generado por la API en ejecución.</summary>
    public JsonDocument DocumentoApi { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // MapOpenApi() sólo se expone en Development (Program.cs).
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Valores mínimos para que las opciones con ValidateOnStart no aborten el arranque.
                    // No se usa la base de datos: sin cadena de conexión, Program.cs omite el health
                    // check de SQL Server y el DbContext no se conecta durante el arranque.
                    ["ConnectionStrings:SqlServer"] = string.Empty,
                    ["Jwt:SigningKey"] = "clave-de-pruebas-de-contrato-con-longitud-suficiente-256-bits",
                    // BootstrapOptions también se valida al arrancar (RF-078). Sin cadena de conexión
                    // la siembra se omite, pero las opciones deben estar completas o el host aborta.
                    ["Bootstrap:AdminEmail"] = "contrato-admin@pruebas.local",
                    ["Bootstrap:AdminPassword"] = "Contrato-Pruebas1",
                }));
        });

        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        respuesta.EnsureSuccessStatusCode();

        DocumentoApi = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
    }

    public Task DisposeAsync()
    {
        DocumentoApi?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Carga y deserializa un contrato YAML de la carpeta <c>contracts/</c>.</summary>
    public static ContratoOpenApi CargarContrato(string nombreArchivo)
    {
        var ruta = Path.Combine(RaizContratos(), nombreArchivo);

        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException(
                $"No se encontró el contrato '{nombreArchivo}'. Ruta resuelta: {ruta}.",
                ruta);
        }

        var deserializador = new DeserializerBuilder().Build();
        var crudo = deserializador.Deserialize<Dictionary<object, object>>(File.ReadAllText(ruta));

        return new ContratoOpenApi(nombreArchivo, crudo);
    }

    /// <summary>
    /// Localiza <c>specs/.../contracts</c> subiendo desde el directorio de ejecución de las pruebas.
    /// </summary>
    /// <remarks>
    /// Se resuelve en tiempo de ejecución en lugar de copiar los contratos al directorio de salida:
    /// así la prueba siempre se ejecuta contra el contrato vigente del repositorio y no contra una
    /// copia que podría quedar desactualizada sin que nadie lo note.
    /// </remarks>
    private static string RaizContratos()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(
                directorio.FullName,
                "specs",
                "001-control-acceso-empresarial",
                "contracts");

            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encontró la carpeta specs/001-control-acceso-empresarial/contracts partiendo de " +
            AppContext.BaseDirectory);
    }
}

/// <summary>Vista tipada mínima de un contrato OpenAPI en YAML.</summary>
public sealed class ContratoOpenApi(string nombre, Dictionary<object, object> crudo)
{
    public string Nombre { get; } = nombre;

    /// <summary>Todas las operaciones declaradas, como pares (ruta, método HTTP en minúsculas).</summary>
    public IReadOnlyList<(string Ruta, string Metodo)> Operaciones { get; } = LeerOperaciones(crudo);

    /// <summary>Códigos de estado declarados por operación.</summary>
    public IReadOnlyDictionary<(string Ruta, string Metodo), IReadOnlyList<string>> Estados { get; } =
        LeerEstados(crudo);

    /// <summary>Nombres de propiedad declarados por cada esquema de <c>components.schemas</c>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PropiedadesPorEsquema { get; } =
        LeerEsquemas(crudo);

    /// <summary>
    /// Valores declarados por cada esquema de <c>components.schemas</c> que sea un enumerado.
    /// </summary>
    /// <remarks>
    /// Un enumerado no tiene <c>properties</c>, así que no aparece en
    /// <see cref="PropiedadesPorEsquema"/>. Se lee del YAML —y no del texto crudo— para que dé igual
    /// si el contrato lo escribe en línea (<c>enum: [A, B]</c>) o en bloque.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValoresPorEnumerado { get; } =
        LeerEnumerados(crudo);

    private readonly Dictionary<object, object> crudoDelContrato = crudo;

    /// <summary>
    /// Nombres de los parámetros <c>in: query</c> declarados por una operación.
    /// </summary>
    /// <remarks>
    /// El snapshot transversal compara rutas, estados y cuerpos, no parámetros de consulta: sin esta
    /// lectura, un filtro declarado en el contrato y no implementado pasaría inadvertido. Resuelve los
    /// <c>$ref</c> a <c>components.parameters</c>, que es como el contrato declara la paginación.
    /// </remarks>
    public IReadOnlyList<string> ParametrosDeConsulta(string ruta, string metodo)
    {
        if (Paths(crudoDelContrato).GetValueOrDefault(ruta) is not Dictionary<object, object> item
            || item.GetValueOrDefault(metodo) is not Dictionary<object, object> operacion
            || operacion.GetValueOrDefault("parameters") is not List<object> parametros)
        {
            return [];
        }

        var nombres = new List<string>();

        foreach (var entrada in parametros.OfType<Dictionary<object, object>>())
        {
            var declaracion = entrada.GetValueOrDefault("$ref") is string referencia
                ? Referenciado(crudoDelContrato, referencia)
                : entrada;

            if (declaracion?.GetValueOrDefault("in") as string != "query")
            {
                continue;
            }

            if (Convert.ToString(declaracion.GetValueOrDefault("name"), CultureInfo.InvariantCulture)
                is { Length: > 0 } nombre)
            {
                nombres.Add(nombre);
            }
        }

        return nombres;
    }

    /// <summary>Resuelve un <c>$ref</c> local del tipo <c>#/components/parameters/Pagina</c>.</summary>
    private static Dictionary<object, object>? Referenciado(
        Dictionary<object, object> crudo,
        string referencia)
    {
        var partes = referencia.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Se salta el "#" inicial y se desciende por el resto del camino.
        object? actual = crudo;

        foreach (var parte in partes.Where(p => p != "#"))
        {
            if (actual is not Dictionary<object, object> mapa)
            {
                return null;
            }

            actual = mapa.GetValueOrDefault(parte);
        }

        return actual as Dictionary<object, object>;
    }

    private static readonly string[] MetodosHttp =
        ["get", "post", "put", "patch", "delete", "head", "options"];

    private static Dictionary<object, object> Paths(Dictionary<object, object> crudo) =>
        crudo.TryGetValue("paths", out var paths) && paths is Dictionary<object, object> mapa
            ? mapa
            : [];

    private static List<(string, string)> LeerOperaciones(Dictionary<object, object> crudo)
    {
        var operaciones = new List<(string, string)>();

        foreach (var (ruta, valor) in Paths(crudo))
        {
            if (valor is not Dictionary<object, object> item)
            {
                continue;
            }

            foreach (var (clave, _) in item)
            {
                var metodo = Convert.ToString(clave, CultureInfo.InvariantCulture)!;
                if (MetodosHttp.Contains(metodo, StringComparer.Ordinal))
                {
                    operaciones.Add((Convert.ToString(ruta, CultureInfo.InvariantCulture)!, metodo));
                }
            }
        }

        return operaciones;
    }

    private static Dictionary<(string, string), IReadOnlyList<string>> LeerEstados(
        Dictionary<object, object> crudo)
    {
        var estados = new Dictionary<(string, string), IReadOnlyList<string>>();

        foreach (var (ruta, valor) in Paths(crudo))
        {
            if (valor is not Dictionary<object, object> item)
            {
                continue;
            }

            foreach (var (clave, operacion) in item)
            {
                var metodo = Convert.ToString(clave, CultureInfo.InvariantCulture)!;

                if (!MetodosHttp.Contains(metodo, StringComparer.Ordinal) ||
                    operacion is not Dictionary<object, object> detalle ||
                    !detalle.TryGetValue("responses", out var respuestas) ||
                    respuestas is not Dictionary<object, object> mapaRespuestas)
                {
                    continue;
                }

                estados[(Convert.ToString(ruta, CultureInfo.InvariantCulture)!, metodo)] =
                    [.. mapaRespuestas.Keys.Select(k => Convert.ToString(k, CultureInfo.InvariantCulture)!)];
            }
        }

        return estados;
    }

    private static Dictionary<string, IReadOnlyList<string>> LeerEnumerados(
        Dictionary<object, object> crudo)
    {
        var enumerados = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var (nombre, definicion) in Esquemas(crudo))
        {
            if (definicion is not Dictionary<object, object> detalle ||
                !detalle.TryGetValue("enum", out var valores) ||
                valores is not List<object> lista)
            {
                continue;
            }

            enumerados[Convert.ToString(nombre, CultureInfo.InvariantCulture)!] =
                [.. lista.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)!)];
        }

        return enumerados;
    }

    private static Dictionary<object, object> Esquemas(Dictionary<object, object> crudo) =>
        crudo.TryGetValue("components", out var componentes)
        && componentes is Dictionary<object, object> mapaComponentes
        && mapaComponentes.TryGetValue("schemas", out var declarados)
        && declarados is Dictionary<object, object> mapaEsquemas
            ? mapaEsquemas
            : [];

    private static Dictionary<string, IReadOnlyList<string>> LeerEsquemas(Dictionary<object, object> crudo)
    {
        var esquemas = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        if (!crudo.TryGetValue("components", out var componentes) ||
            componentes is not Dictionary<object, object> mapaComponentes ||
            !mapaComponentes.TryGetValue("schemas", out var declarados) ||
            declarados is not Dictionary<object, object> mapaEsquemas)
        {
            return esquemas;
        }

        foreach (var (nombre, definicion) in mapaEsquemas)
        {
            if (definicion is not Dictionary<object, object> detalle ||
                !detalle.TryGetValue("properties", out var propiedades) ||
                propiedades is not Dictionary<object, object> mapaPropiedades)
            {
                continue;
            }

            esquemas[Convert.ToString(nombre, CultureInfo.InvariantCulture)!] =
                [.. mapaPropiedades.Keys.Select(k => Convert.ToString(k, CultureInfo.InvariantCulture)!)];
        }

        return esquemas;
    }
}

/// <summary>Comparte una única instancia de la API en memoria entre las clases de prueba de contrato.</summary>
[CollectionDefinition(Name)]
public sealed class ApiContratoFixtureDefinition : ICollectionFixture<ApiContratoFixture>
{
    public const string Name = "ApiContrato";
}
