using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EnterpriseAccessControl.IntegrationTests.Fixtures;

/// <summary>
/// Levanta la API completa apuntando al SQL Server del contenedor de pruebas.
/// </summary>
/// <remarks>
/// No se sustituye ningún servicio por un doble: el objetivo es ejercitar el mismo grafo de
/// dependencias que se ejecuta en producción —interceptor de auditoría, política de autorización,
/// middleware de errores— contra un motor real. Sólo se sobrescribe la configuración (cadena de
/// conexión y clave de firma).
/// </remarks>
public sealed class ApiFactory(string cadenaConexion) : WebApplicationFactory<Program>
{
    public const string ClaveFirmaPruebas =
        "clave-de-firma-exclusiva-de-pruebas-de-integracion-con-256-bits-minimo";

    /// <summary>
    /// Credenciales de arranque exclusivas de las pruebas (RF-078).
    /// </summary>
    /// <remarks>
    /// <c>BootstrapOptions</c> se valida al arrancar, así que la API no levanta sin estos valores.
    /// Son valores de prueba, no un secreto de despliegue: el entorno real los recibe por variable de
    /// entorno o gestor de secretos, nunca desde el repositorio.
    /// </remarks>
    public const string CorreoBootstrapPruebas = "bootstrap-admin@pruebas.local";

    public const string PasswordBootstrapPruebas = "Bootstrap-Pruebas1";

    /// <summary>Opciones de JSON equivalentes a las de la API, para deserializar sus respuestas.</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = cadenaConexion,
                ["Jwt:SigningKey"] = ClaveFirmaPruebas,
                ["Bootstrap:AdminEmail"] = CorreoBootstrapPruebas,
                ["Bootstrap:AdminPassword"] = PasswordBootstrapPruebas,
            }));
    }

    /// <summary>Cliente HTTP que no sigue redirecciones, para poder observar los 3xx tal cual.</summary>
    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
    });

    /// <summary>Cliente autenticado con un token emitido para el usuario indicado.</summary>
    /// <remarks>
    /// El rol y las compañías se derivan de las asignaciones **vigentes** del usuario, igual que en el
    /// login real (RF-074, RF-077): así el token de una prueba no puede otorgar un alcance que el
    /// modelo de datos no respalda.
    /// </remarks>
    public async Task<HttpClient> CrearClienteAutenticadoAsync(Guid usuarioId, string correo)
    {
        var (rol, companiaIds) = await AlcanceDeAsync(usuarioId);

        using var ambito = Services.CreateScope();
        var tokens = ambito.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokens.Emitir(usuarioId, correo, rol, companiaIds);

        var cliente = CrearCliente();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return cliente;
    }

    /// <summary>Inserta un usuario con contraseña ya hasheada por el hasher real de la aplicación.</summary>
    /// <remarks>
    /// <paramref name="alcanceCompanias"/> crea una asignación <c>COMPANY_ADMINISTRATOR</c> vigente
    /// por compañía. Para sembrar un administrador global se usa <paramref name="global"/>, que no
    /// enumera compañías (RF-074).
    /// </remarks>
    public async Task<Usuario> SembrarUsuarioAsync(
        string correo,
        string password,
        EstadoUsuario estado = EstadoUsuario.ACTIVO,
        bool requiereCambioPassword = false,
        DateTime? fechaUltimoCambioPassword = null,
        IReadOnlyList<Guid>? alcanceCompanias = null,
        bool global = false)
    {
        using var ambito = Services.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = ambito.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var usuario = new Usuario
        {
            Correo = correo,
            PasswordHash = hasher.Hash(password),
            Estado = estado,
            RequiereCambioPassword = requiereCambioPassword,
            FechaUltimoCambioPassword = fechaUltimoCambioPassword ?? DateTime.UtcNow,
        };

        db.Set<Usuario>().Add(usuario);

        var ahora = DateTime.UtcNow;

        if (global)
        {
            db.Set<AsignacionRolAdministrativo>().Add(new AsignacionRolAdministrativo
            {
                UsuarioId = usuario.Id,
                Rol = RolAdministrativo.GLOBAL_ADMINISTRATOR,
                CompaniaId = null,
                FechaHoraInicio = ahora.AddDays(-1),
                FechaHoraFin = ahora.AddYears(1),
            });
        }

        foreach (var companiaId in alcanceCompanias ?? [])
        {
            db.Set<AsignacionRolAdministrativo>().Add(new AsignacionRolAdministrativo
            {
                UsuarioId = usuario.Id,
                Rol = RolAdministrativo.COMPANY_ADMINISTRATOR,
                CompaniaId = companiaId,
                FechaHoraInicio = ahora.AddDays(-1),
                // RF-075: la vigencia es obligatoria desde la creación.
                FechaHoraFin = ahora.AddYears(1),
            });
        }

        await db.SaveChangesAsync();

        return usuario;
    }

    /// <summary>Inserta una compañía mínima utilizable como destino de un alcance administrativo.</summary>
    public async Task<Compania> SembrarCompaniaAsync(
        string nombre,
        TipoCompania tipo = TipoCompania.PRINCIPAL_MANDANTE,
        Estado estado = Estado.ACTIVO,
        string? zonaHorariaIana = null)
    {
        using var ambito = Services.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        var compania = new Compania
        {
            Nombre = nombre,
            // Una Principal siempre tiene zona propia (RF-080); una Contratista no la necesita.
            ZonaHorariaIana = zonaHorariaIana
                ?? (tipo == TipoCompania.PRINCIPAL_MANDANTE ? "America/Lima" : null),
            // El maestro TipoDocumento se implementa en una fase posterior y todavía no hay FK: para
            // US1 basta un identificador cualquiera, porque lo que se ejercita aquí es el alcance.
            TipoDocumentoId = Guid.CreateVersion7(),
            // Documento único por compañía sembrada: evita chocar con el índice único entre pruebas.
            NumeroDocumento = Guid.CreateVersion7().ToString("N")[..12],
            TipoCompania = tipo,
            Estado = estado,
        };

        db.Set<Compania>().Add(compania);
        await db.SaveChangesAsync();

        return compania;
    }

    /// <summary>
    /// Identificadores de los valores de catálogo sembrados para Perú (migración SeedMaestrosPeru).
    /// </summary>
    /// <remarks>
    /// Se usan los de la semilla en lugar de crear catálogos por prueba: así las pruebas ejercitan
    /// exactamente los datos con los que arranca un entorno real.
    /// </remarks>
    public static class Maestros
    {
        public static readonly Guid Dni = new("0199b0d0-0001-7000-8000-000000000001");
        public static readonly Guid Pasaporte = new("0199b0d0-0001-7000-8000-000000000003");
        public static readonly Guid OPositivo = new("0199b0d0-0002-7000-8000-000000000001");
        public static readonly Guid Masculino = new("0199b0d0-0003-7000-8000-000000000001");
    }

    /// <summary>Inserta una persona y, opcionalmente, su pertenencia vigente a una compañía.</summary>
    public async Task<Persona> SembrarPersonaAsync(
        string? numeroDocumento = null,
        Guid? companiaId = null,
        string apellidos = "Pérez",
        string nombres = "Ana",
        DateTime? inicioVigencia = null,
        DateTime? finVigencia = null)
    {
        using var ambito = Services.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        var persona = new Persona
        {
            Nombres = nombres,
            Apellidos = apellidos,
            FechaNacimiento = new DateOnly(1990, 5, 20),
            TipoDocumentoId = Maestros.Dni,
            NumeroDocumento = numeroDocumento ?? Guid.CreateVersion7().ToString("N")[..12],
            GeneroId = Maestros.Masculino,
            CorreoElectronico = $"{Guid.CreateVersion7():N}@empresa.cl",
            TipoSangreId = Maestros.OPositivo,
            ContactoEmergencia = "Contacto de prueba",
            NumeroEmergencia = "+51 999 999 999",
        };

        db.Set<Persona>().Add(persona);

        if (companiaId is not null)
        {
            var ahora = DateTime.UtcNow;

            db.Set<AsignacionPersonaCompania>().Add(new AsignacionPersonaCompania
            {
                PersonaId = persona.Id,
                CompaniaId = companiaId.Value,
                FechaHoraInicio = inicioVigencia ?? ahora.AddDays(-1),
                // RF-071: la fecha de fin es obligatoria desde la creación.
                FechaHoraFin = finVigencia ?? ahora.AddYears(1),
            });
        }

        await db.SaveChangesAsync();

        return persona;
    }

    /// <summary>Rol y compañías vigentes del usuario, tal como los resolvería el login (RF-077).</summary>
    public async Task<(RolAdministrativo? Rol, IReadOnlyList<Guid> CompaniaIds)> AlcanceDeAsync(
        Guid usuarioId)
    {
        using var ambito = Services.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        var ahora = DateTime.UtcNow;

        var vigentes = await db.Set<AsignacionRolAdministrativo>()
            .Where(a => a.UsuarioId == usuarioId
                        && a.FechaHoraInicio <= ahora
                        && ahora < a.FechaHoraFin)
            .ToListAsync();

        if (vigentes.Count == 0)
        {
            return (null, []);
        }

        if (vigentes.Exists(a => a.Rol == RolAdministrativo.GLOBAL_ADMINISTRATOR))
        {
            return (RolAdministrativo.GLOBAL_ADMINISTRATOR, []);
        }

        return (
            RolAdministrativo.COMPANY_ADMINISTRATOR,
            vigentes.Where(a => a.CompaniaId is not null).Select(a => a.CompaniaId!.Value).Distinct().ToList());
    }

    /// <summary>Ejecuta una acción con un <see cref="AppDbContext"/> del contenedor de servicios real.</summary>
    public async Task ConDbContextAsync(Func<AppDbContext, Task> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);

        using var ambito = Services.CreateScope();
        await accion(ambito.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
