using EnterpriseAccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Fixtures;

/// <summary>
/// Padrón de volumen de CS-002 y CS-003: al menos 100.000 personas con pertenencia vigente a una
/// única compañía.
/// </summary>
/// <remarks>
/// Compartido por las pruebas de rendimiento de búsqueda (US4) y de evaluación de acceso (US8), que
/// según spec.md se miden sobre el mismo escenario de ≥100.000 personas.
///
/// El volumen se carga con <c>INSERT ... SELECT</c> generativo en lugar de inserciones por EF Core:
/// lo que se quiere medir es el coste de las consultas, y sembrar fila a fila multiplicaría la
/// duración de la suite sin aportar nada a esa medición. La carga es idempotente dentro de un mismo
/// contenedor.
/// </remarks>
internal static class VolumenPersonas
{
    public const int PersonasObjetivo = 100_000;

    private const string NombreCompania = "Padrón de volumen CS-002";

    /// <summary>
    /// Compañía única a la que se vincula todo el padrón de volumen, creada una sola vez.
    /// </summary>
    /// <remarks>
    /// Debe ser la misma para todas las pruebas: RF-014 permite una única pertenencia activa por
    /// persona, así que vincular el mismo padrón a dos compañías produciría un solapamiento auténtico
    /// que el trigger rechaza.
    /// </remarks>
    public static async Task<Guid> ObtenerCompaniaAsync(ApiFactory api)
    {
        Guid? existente = null;

        await api.ConDbContextAsync(async db =>
        {
            existente = await db.Set<Compania>().AsNoTracking()
                .Where(c => c.Nombre == NombreCompania)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync();
        });

        if (existente is not null)
        {
            return existente.Value;
        }

        return (await api.SembrarCompaniaAsync(NombreCompania)).Id;
    }

    /// <summary>
    /// Carga el volumen y devuelve cuántas personas quedan con pertenencia a esa compañía.
    /// </summary>
    public static async Task<int> SembrarAsync(ApiFactory api, Guid companiaId)
    {
        var visibles = 0;

        await api.ConDbContextAsync(async db =>
        {
            // El objetivo se calcula sobre las personas que acabarán siendo VISIBLES para esta
            // compañía, no sobre el total de la tabla: otras pruebas de la colección crean personas
            // con pertenencia propia, y ésas nunca se vincularán aquí (RF-014).
            var yaVinculadas = await db.Set<AsignacionPersonaCompania>()
                .CountAsync(a => a.CompaniaId == companiaId);

            var sinPertenencia = await db.Set<Persona>()
                .CountAsync(p => !db.Set<AsignacionPersonaCompania>().Any(a => a.PersonaId == p.Id));

            var faltantes = PersonasObjetivo - yaVinculadas - sinPertenencia;

            if (faltantes > 0)
            {
                await InsertarPersonasAsync(db, faltantes);
            }

            // Se vinculan solo las personas que no tienen NINGUNA pertenencia: una segunda
            // pertenencia activa para la misma persona violaría RF-014 y el trigger la rechazaría.
            await VincularPersonasAsync(db, companiaId);

            visibles = await db.Set<AsignacionPersonaCompania>()
                .CountAsync(a => a.CompaniaId == companiaId);
        });

        return visibles;
    }

    private static async Task InsertarPersonasAsync(
        Infrastructure.Persistence.AppDbContext db,
        int cantidad)
    {
        // Una tabla de números derivada del catálogo del sistema: genera las filas en el servidor,
        // sin round-trips ni materializar nada en el cliente.
        var sql = """
            WITH Numeros AS (
                SELECT TOP (@cantidad)
                       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                FROM sys.all_objects a
                CROSS JOIN sys.all_objects b
            )
            INSERT INTO [Persona]
                ([Id], [Nombres], [Apellidos], [FechaNacimiento], [TipoDocumentoId],
                 [NumeroDocumento], [GeneroId], [CorreoElectronico], [TipoSangreId],
                 [ContactoEmergencia], [NumeroEmergencia], [CreatedAt], [UpdatedAt],
                 [CreatedById], [UpdatedById])
            SELECT
                NEWID(),
                CONCAT('Nombre', n),
                CONCAT('Apellido', n),
                DATEADD(DAY, -(n % 20000) - 7000, CAST(SYSUTCDATETIME() AS date)),
                @tipoDocumento,
                CONCAT('V', RIGHT(CONCAT('000000000', CAST(@base AS varchar(20)), n), 18)),
                @genero,
                CONCAT('persona', n, '@volumen.cl'),
                @tipoSangre,
                CONCAT('Contacto', n),
                '+51 999 000 000',
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                NULL,
                NULL
            FROM Numeros;
            """;

        await db.Database.ExecuteSqlRawAsync(
            sql,
            new Microsoft.Data.SqlClient.SqlParameter("@cantidad", cantidad),
            new Microsoft.Data.SqlClient.SqlParameter("@tipoDocumento", ApiFactory.Maestros.Dni),
            new Microsoft.Data.SqlClient.SqlParameter("@genero", ApiFactory.Maestros.Masculino),
            new Microsoft.Data.SqlClient.SqlParameter("@tipoSangre", ApiFactory.Maestros.OPositivo),
            new Microsoft.Data.SqlClient.SqlParameter(
                "@base",
                DateTime.UtcNow.Ticks % 1_000_000L));
    }

    private static async Task VincularPersonasAsync(
        Infrastructure.Persistence.AppDbContext db,
        Guid companiaId)
    {
        var sql = """
            INSERT INTO [AsignacionPersonaCompania]
                ([Id], [PersonaId], [CompaniaId], [FechaHoraInicio], [FechaHoraFin],
                 [CreatedAt], [UpdatedAt], [CreatedById], [UpdatedById])
            SELECT
                NEWID(),
                p.[Id],
                @compania,
                DATEADD(DAY, -30, SYSUTCDATETIME()),
                DATEADD(YEAR, 1, SYSUTCDATETIME()),
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                NULL,
                NULL
            FROM [Persona] AS p
            WHERE NOT EXISTS (
                SELECT 1 FROM [AsignacionPersonaCompania] AS a
                WHERE a.[PersonaId] = p.[Id]);
            """;

        await db.Database.ExecuteSqlRawAsync(
            sql,
            new Microsoft.Data.SqlClient.SqlParameter("@compania", companiaId));
    }
}
