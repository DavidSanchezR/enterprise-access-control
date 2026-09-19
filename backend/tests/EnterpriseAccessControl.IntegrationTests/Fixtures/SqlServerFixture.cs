using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Infrastructure.Auditing;
using EnterpriseAccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace EnterpriseAccessControl.IntegrationTests.Fixtures;

/// <summary>
/// Levanta una instancia real de SQL Server en contenedor para las pruebas de integración
/// (research.md §11).
/// </summary>
/// <remarks>
/// **Por qué una base de datos real y no un proveedor en memoria**: las garantías centrales de
/// integridad temporal de este dominio viven en triggers <c>AFTER INSERT, UPDATE</c> de SQL Server
/// (research.md §5), que no tienen representación alguna en el modelo de EF Core. Un proveedor
/// en memoria las ignoraría por completo y las pruebas de no-solapamiento pasarían sin verificar
/// nada. Lo mismo aplica a <c>rowversion</c> (§16) y a los CTE recursivos de jerarquía (§4).
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Misma imagen que docker-compose.yml, para que las pruebas se ejecuten contra exactamente el
    // mismo motor que el entorno de desarrollo y el de despliegue (plan.md, Target Platform).
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>La API completa, ya apuntando a este contenedor.</summary>
    public ApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // El esquema se crea aplicando las migraciones reales — las mismas que se ejecutarán en
        // producción, incluidos los triggers añadidos vía SQL crudo. Nunca EnsureCreated(), que
        // saltaría las migraciones y produciría un esquema distinto al real.
        await using var db = CrearDbContext();
        await db.Database.MigrateAsync();

        // La API se construye después de migrar: así ningún arranque observa un esquema a medias.
        Api = new ApiFactory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        Api?.Dispose();
        await _container.DisposeAsync();
    }

    /// <summary>Crea un contexto apuntando al contenedor, con auditoría estampada por el usuario indicado.</summary>
    public AppDbContext CrearDbContext(Guid? usuarioId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new UsuarioActualFijo(usuarioId),
                    new RelojSistemaFijo()))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class UsuarioActualFijo(Guid? usuarioId) : IUsuarioActualAccessor
    {
        public Guid? UsuarioId { get; } = usuarioId;
    }

    private sealed class RelojSistemaFijo : IRelojSistema
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}

/// <summary>
/// Comparte una única instancia de SQL Server entre todas las pruebas de la colección: arrancar un
/// contenedor por clase de prueba multiplicaría el tiempo de la suite sin aportar aislamiento real
/// (cada prueba usa sus propios datos).
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerFixtureDefinition : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
