using System.Diagnostics;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit.Abstractions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// CS-002: la búsqueda de personas debe responder con p95 &lt; 2 s sobre ≥100.000 personas.
/// </summary>
/// <remarks>
/// El volumen lo carga <see cref="VolumenPersonas"/>, compartido con la prueba de rendimiento de la
/// evaluación de acceso (CS-003).
///
/// La cota se comprueba sobre el percentil 95 de una serie de peticiones, no sobre una medición
/// aislada, porque una sola muestra confundiría el coste real con el arranque en frío del plan de
/// ejecución. La primera petición se descarta expresamente por ese motivo.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PersonasPerformanceTests(SqlServerFixture fixture, ITestOutputHelper salida)
{
    private const int Mediciones = 20;
    private static readonly TimeSpan CotaP95 = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task La_busqueda_responde_bajo_dos_segundos_con_cien_mil_personas()
    {
        var compania = await VolumenPersonas.ObtenerCompaniaAsync(fixture.Api);

        var sembradas = await VolumenPersonas.SembrarAsync(fixture.Api, compania);
        salida.WriteLine($"Personas visibles para el alcance de prueba: {sembradas}");

        sembradas.Should().BeGreaterThanOrEqualTo(
            VolumenPersonas.PersonasObjetivo, "CS-002 fija el escenario en al menos 100.000 personas");

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"volumen.{Guid.CreateVersion7():N}@empresa.cl",
            "Contrasena1Segura",
            alcanceCompanias: [compania]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        // Descartada: paga la compilación del plan y el calentamiento de la caché de datos.
        await MedirAsync(cliente, "?texto=Apellido&pagina=1&tamañoPagina=20");

        var muestras = new List<TimeSpan>(Mediciones);

        for (var i = 0; i < Mediciones; i++)
        {
            // Se varía la página para no medir siempre el mismo rango ya cacheado.
            var pagina = (i % 5) + 1;
            muestras.Add(await MedirAsync(cliente, $"?texto=Apellido&pagina={pagina}&tamañoPagina=20"));
        }

        muestras.Sort();

        var p95 = muestras[(int)Math.Ceiling(muestras.Count * 0.95) - 1];
        var mediana = muestras[muestras.Count / 2];

        salida.WriteLine(
            $"mediana={mediana.TotalMilliseconds:F0} ms · p95={p95.TotalMilliseconds:F0} ms · " +
            $"máx={muestras[^1].TotalMilliseconds:F0} ms");

        p95.Should().BeLessThan(
            CotaP95,
            "CS-002 exige p95 < 2 s con {0} personas", sembradas);
    }

    [Fact]
    public async Task La_busqueda_por_documento_exacto_es_rapida_con_volumen()
    {
        // El caso más frecuente en portería es la búsqueda por documento exacto; debe resolverse
        // por índice y no degradarse con el volumen.
        // Se reutiliza la misma compañía de volumen: RF-014 admite una sola pertenencia activa por
        // persona, de modo que vincular el padrón a una segunda compañía sería un solapamiento real
        // —y el trigger lo rechazaría, con razón—.
        var compania = await VolumenPersonas.ObtenerCompaniaAsync(fixture.Api);

        await VolumenPersonas.SembrarAsync(fixture.Api, compania);

        var persona = await fixture.Api.SembrarPersonaAsync(companiaId: compania);

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"doc.{Guid.CreateVersion7():N}@empresa.cl",
            "Contrasena1Segura",
            alcanceCompanias: [compania]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        await MedirAsync(cliente, $"?texto={persona.NumeroDocumento}");

        var duracion = await MedirAsync(cliente, $"?texto={persona.NumeroDocumento}");

        salida.WriteLine($"búsqueda por documento exacto: {duracion.TotalMilliseconds:F0} ms");

        duracion.Should().BeLessThan(CotaP95);
    }

    private static async Task<TimeSpan> MedirAsync(HttpClient cliente, string consulta)
    {
        var reloj = Stopwatch.StartNew();

        var pagina = await cliente.GetFromJsonAsync<PaginaResponse<PersonaDto>>(
            new Uri($"/api/personas{consulta}", UriKind.Relative), ApiFactory.Json);

        reloj.Stop();

        // Se valida que la respuesta sea real: medir una consulta que devuelve vacío por error no
        // mediría nada.
        pagina.Should().NotBeNull();

        return reloj.Elapsed;
    }
}
