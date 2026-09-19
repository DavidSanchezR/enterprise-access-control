using System.Diagnostics;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// CS-003: la evaluación de acceso responde con p95 &lt; 500 ms contra SQL Server, sobre el escenario
/// de volumen de ≥100.000 personas e incluyendo el paso de credencial.
/// </summary>
/// <remarks>
/// Sobre el padrón de <see cref="VolumenPersonas"/> se cargan, para una parte de las personas, todas
/// las filas que el algoritmo consulta: contexto operativo, credencial, perfil, y un permiso personal
/// con sus bloques horarios **sobre la misma área**. Ese último punto es deliberado: el paso 10 lee los
/// permisos de un área, y una sola área con decenas de miles de permisos es el caso que podría
/// degradar la consulta si no usara bien sus índices.
///
/// Cada muestra evalúa a una persona distinta que obtiene CONCEDIDO, es decir, que recorre los 14
/// pasos completos: medir denegaciones tempranas daría tiempos optimistas.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class EvaluacionAccesoPerformanceTests(SqlServerFixture fixture, ITestOutputHelper salida)
{
    private const int PersonasConAcceso = 20_000;
    private const int Mediciones = 30;
    private const string NombreArea = "Área de volumen CS-003";
    private static readonly TimeSpan CotaP95 = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task La_evaluacion_completa_responde_con_p95_bajo_500_ms_con_volumen()
    {
        var principalId = await VolumenPersonas.ObtenerCompaniaAsync(fixture.Api);
        var personas = await VolumenPersonas.SembrarAsync(fixture.Api, principalId);

        personas.Should().BeGreaterThanOrEqualTo(VolumenPersonas.PersonasObjetivo);

        var areaId = await SembrarVolumenDeEvaluacionAsync(principalId);
        var volumen = await ContarVolumenAsync(areaId);

        salida.WriteLine(
            $"personas={personas} · permisos en el área={volumen.Permisos} · bloques={volumen.Bloques} · " +
            $"contextos={volumen.Contextos} · credenciales={volumen.Credenciales}");

        volumen.Permisos.Should().BeGreaterThanOrEqualTo(PersonasConAcceso);

        var admin = await fixture.Api.SembrarUsuarioAsync(
            $"cs003.{Guid.CreateVersion7():N}@empresa.cl",
            "Contrasena1Segura",
            alcanceCompanias: [principalId]);

        using var cliente = await fixture.Api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);

        var muestra = await PersonasDeMuestraAsync(areaId, Mediciones + 1);

        // Descartada: paga la compilación de los planes y el calentamiento de la caché de datos.
        await MedirAsync(cliente, muestra[0], areaId);

        var tiempos = new List<TimeSpan>(Mediciones);

        foreach (var personaId in muestra.Skip(1))
        {
            tiempos.Add(await MedirAsync(cliente, personaId, areaId));
        }

        tiempos.Sort();

        var p95 = tiempos[(int)Math.Ceiling(tiempos.Count * 0.95) - 1];
        var mediana = tiempos[tiempos.Count / 2];

        salida.WriteLine(
            $"mediana={mediana.TotalMilliseconds:F0} ms · p95={p95.TotalMilliseconds:F0} ms · " +
            $"máx={tiempos[^1].TotalMilliseconds:F0} ms");

        p95.Should().BeLessThan(CotaP95, "CS-003 exige p95 < 500 ms en la evaluación de acceso");
    }

    private static async Task<TimeSpan> MedirAsync(HttpClient cliente, Guid personaId, Guid areaId)
    {
        var reloj = Stopwatch.StartNew();

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/evaluacion-acceso", UriKind.Relative),
            new EvaluarAccesoRequest(personaId, areaId, EscenarioPermisos.Instante),
            ApiFactory.Json);

        var resultado = await respuesta.Content.ReadFromJsonAsync<EvaluarAccesoResponse>(ApiFactory.Json);

        reloj.Stop();

        // Una medición solo vale si recorrió el algoritmo completo: un DENEGADO temprano sería más
        // rápido y haría pasar la cota sin medir lo que CS-003 pide.
        respuesta.EnsureSuccessStatusCode();
        resultado!.MotivoDenegacion.Should().BeNull();
        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        return reloj.Elapsed;
    }

    /// <summary>
    /// Área, perfil y tipo de credencial de volumen, y para <see cref="PersonasConAcceso"/> personas del
    /// padrón su contexto, credencial, perfil y permiso con bloques. Idempotente.
    /// </summary>
    /// <remarks>
    /// Las fechas de cada fila se copian de la pertenencia de la persona: así se respeta la contención
    /// temporal de RF-072 aunque la carga no pase por el servicio que la valida.
    /// </remarks>
    private async Task<Guid> SembrarVolumenDeEvaluacionAsync(Guid principalId)
    {
        var areaId = Guid.Empty;

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var existente = await db.Set<AreaAcceso>().AsNoTracking()
                .Where(a => a.Nombre == NombreArea)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync();

            if (existente is not null)
            {
                areaId = existente.Value;
                return;
            }

            var area = new AreaAcceso { Nombre = NombreArea, CompaniaPrincipalId = principalId };
            var perfil = new TipoPersona { Nombre = "Perfil volumen CS-003", Estado = Estado.ACTIVO };
            var tipoCredencial = new TipoCredencial { Nombre = "Fotocheck volumen CS-003", Estado = Estado.ACTIVO };

            db.Add(area);
            db.Add(perfil);
            db.Add(tipoCredencial);
            db.Add(new AreaAccesoTipoPersona { AreaAccesoId = area.Id, TipoPersonaId = perfil.Id });

            await db.SaveChangesAsync();
            areaId = area.Id;

            var parametros = new object[]
            {
                new SqlParameter("@cantidad", PersonasConAcceso),
                new SqlParameter("@principal", principalId),
                new SqlParameter("@area", area.Id),
                new SqlParameter("@perfil", perfil.Id),
                new SqlParameter("@tipoCredencial", tipoCredencial.Id),
            };

            // Tabla temporal con las personas elegidas y las fechas de su pertenencia, reutilizada por
            // cada INSERT para que todas las filas cuelguen de las mismas personas.
            await db.Database.ExecuteSqlRawAsync(
                """
                SELECT TOP (@cantidad) a.[PersonaId], a.[FechaHoraInicio], a.[FechaHoraFin]
                INTO #Elegidas
                FROM [AsignacionPersonaCompania] a
                WHERE a.[CompaniaId] = @principal
                ORDER BY a.[PersonaId];

                INSERT INTO [ContextoOperativoPersonaPrincipal]
                    ([Id], [PersonaId], [CompaniaPrincipalId], [FechaHoraInicio], [FechaHoraFin],
                     [Estado], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), e.[PersonaId], @principal, e.[FechaHoraInicio], e.[FechaHoraFin],
                       'ACTIVO', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM #Elegidas e;

                INSERT INTO [AsignacionCredencial]
                    ([Id], [PersonaId], [CompaniaPrincipalId], [TipoCredencialId], [FechaHoraInicio],
                     [FechaHoraFin], [Estado], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), e.[PersonaId], @principal, @tipoCredencial, e.[FechaHoraInicio],
                       e.[FechaHoraFin], 'ASIGNADO', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM #Elegidas e;

                INSERT INTO [AsignacionTipoPersona]
                    ([Id], [PersonaId], [TipoPersonaId], [FechaHoraInicio], [FechaHoraFin], [Estado],
                     [CreatedAt], [UpdatedAt])
                SELECT NEWID(), e.[PersonaId], @perfil, e.[FechaHoraInicio], e.[FechaHoraFin], 'ACTIVO',
                       SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM #Elegidas e;

                INSERT INTO [PermisoAcceso]
                    ([Id], [AreaAccesoId], [Alcance], [PersonaId], [FechaHoraInicioVigencia],
                     [FechaHoraFinVigencia], [Estado], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @area, 'PERSONA', e.[PersonaId], e.[FechaHoraInicio], e.[FechaHoraFin],
                       'ACTIVO', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM #Elegidas e;

                INSERT INTO [BloqueHorarioPermiso]
                    ([Id], [PermisoAccesoId], [DiaSemana], [HoraInicio], [HoraFin], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), p.[Id], d.[Dia], '08:00', '17:00', SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [PermisoAcceso] p
                CROSS JOIN (VALUES ('LUNES'), ('MARTES'), ('MIERCOLES'), ('JUEVES'), ('VIERNES'),
                                   ('SABADO'), ('DOMINGO')) AS d([Dia])
                WHERE p.[AreaAccesoId] = @area;

                DROP TABLE #Elegidas;
                """,
                parametros);
        });

        return areaId;
    }

    private async Task<List<Guid>> PersonasDeMuestraAsync(Guid areaId, int cantidad)
    {
        List<Guid> personas = [];

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var todas = await db.Set<PermisoAcceso>().AsNoTracking()
                .Where(p => p.AreaAccesoId == areaId && p.PersonaId != null)
                .Select(p => p.PersonaId!.Value)
                .ToListAsync();

            // Repartidas por todo el conjunto, no las primeras: evita medir siempre las mismas páginas.
            var paso = Math.Max(1, todas.Count / cantidad);
            personas = [.. todas.Where((_, i) => i % paso == 0).Take(cantidad)];
        });

        return personas;
    }

    private async Task<(int Permisos, int Bloques, int Contextos, int Credenciales)> ContarVolumenAsync(
        Guid areaId)
    {
        (int, int, int, int) conteo = default;

        await fixture.Api.ConDbContextAsync(async db =>
        {
            conteo = (
                await db.Set<PermisoAcceso>().CountAsync(p => p.AreaAccesoId == areaId),
                await db.Set<BloqueHorarioPermiso>().CountAsync(
                    b => db.Set<PermisoAcceso>().Any(p => p.Id == b.PermisoAccesoId && p.AreaAccesoId == areaId)),
                await db.Set<ContextoOperativoPersonaPrincipal>().CountAsync(),
                await db.Set<AsignacionCredencial>().CountAsync());
        });

        return conteo;
    }
}
