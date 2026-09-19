using System.Text.Json;

namespace EnterpriseAccessControl.ContractTests.Infraestructura;

/// <summary>Lectura del documento OpenAPI generado por la API.</summary>
internal static class DocumentoApiExtensions
{
    public static JsonElement Paths(this JsonDocument documento) =>
        documento.RootElement.GetProperty("paths");

    public static bool TieneOperacion(this JsonDocument documento, string ruta, string metodo) =>
        documento.Paths().TryGetProperty(ruta, out var item) &&
        item.TryGetProperty(metodo, out _);

    public static JsonElement Operacion(this JsonDocument documento, string ruta, string metodo) =>
        documento.Paths().GetProperty(ruta).GetProperty(metodo);

    public static IReadOnlyList<string> EstadosDeclarados(
        this JsonDocument documento,
        string ruta,
        string metodo)
    {
        var operacion = documento.Operacion(ruta, metodo);

        return operacion.TryGetProperty("responses", out var respuestas)
            ? [.. respuestas.EnumerateObject().Select(p => p.Name)]
            : [];
    }

    /// <summary>Nombres de propiedad de un esquema de <c>components.schemas</c> del documento generado.</summary>
    public static IReadOnlyList<string> PropiedadesDeEsquema(this JsonDocument documento, string esquema)
    {
        var schemas = documento.RootElement.GetProperty("components").GetProperty("schemas");

        if (!schemas.TryGetProperty(esquema, out var definicion) ||
            !definicion.TryGetProperty("properties", out var propiedades))
        {
            return [];
        }

        return [.. propiedades.EnumerateObject().Select(p => p.Name)];
    }

    /// <summary>
    /// Valores de un enumerado del documento generado, sin el marcador de anulabilidad.
    /// </summary>
    /// <remarks>
    /// Cuando alguna propiedad usa el enum como anulable, el generador de .NET añade <c>null</c> a
    /// la lista de valores del esquema compartido. La anulabilidad real se expresa aparte, en cada
    /// propiedad (<c>oneOf: [null, $ref]</c>), que es lo que el contrato declara; ese <c>null</c> de
    /// la lista es ruido del generador y no un valor de negocio.
    /// </remarks>
    public static IReadOnlyList<string> ValoresDeEnumerado(this JsonDocument documento, string esquema) =>
    [
        .. documento.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(esquema)
            .GetProperty("enum")
            .EnumerateArray()
            .Select(v => v.GetString())
            .OfType<string>(),
    ];

    public static bool TieneEsquema(this JsonDocument documento, string esquema) =>
        documento.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .TryGetProperty(esquema, out _);
}
