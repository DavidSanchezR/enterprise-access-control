using EnterpriseAccessControl.Application.AreaAccess;
using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Application.Masters;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Services;
using EnterpriseAccessControl.Infrastructure.Auditing;
using EnterpriseAccessControl.Infrastructure.Hosting;
using EnterpriseAccessControl.Infrastructure.Persistence;
using EnterpriseAccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.Infrastructure;

/// <summary>
/// Composición de dependencias de la capa de Infraestructura (ASP.NET Core built-in DI, plan.md).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();

        // --- Options Pattern con validación al arranque (research.md §2) ---
        // ValidateOnStart hace que una configuración incompleta falle al iniciar el proceso y no en
        // el primer login o la primera evaluación de acceso.
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<PasswordPolicyOptions>()
            .Bind(configuration.GetSection(PasswordPolicyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<ZonaHorariaOptions>()
            .Bind(configuration.GetSection(ZonaHorariaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Sin valor por defecto versionado: un despliegue sin correo ni contraseña de arranque falla
        // al iniciar en lugar de levantar con un administrador adivinable (RF-078).
        services
            .AddOptions<BootstrapOptions>()
            .Bind(configuration.GetSection(BootstrapOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // --- Contexto de request: identidad y alcance (research.md §3) ---
        services.AddScoped<IUsuarioActualAccessor, UsuarioActualAccessor>();
        services.AddScoped<IAlcanceCompaniaAccessor, AlcanceCompaniaAccessor>();

        // --- Reloj: una sola instancia sirve a ambos puertos ---
        services.AddSingleton<RelojEmpresarial>();
        services.AddSingleton<IRelojEmpresarial>(sp => sp.GetRequiredService<RelojEmpresarial>());
        services.AddSingleton<IRelojSistema>(sp => sp.GetRequiredService<RelojEmpresarial>());

        // --- Persistencia ---
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("SqlServer"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<JerarquiaCicloValidator>();
        services.AddScoped<IJerarquiaConsultas>(sp => sp.GetRequiredService<JerarquiaCicloValidator>());

        // --- Seguridad de credenciales y emisión de token (research.md §2) ---
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // --- Casos de uso de Aplicación (US1) ---
        services.AddScoped<PasswordPolicyValidator>();
        services.AddScoped<AutenticacionService>();
        services.AddScoped<AsignacionRolAdministrativoService>();
        services.AddScoped<UsuarioService>();

        // --- Arranque: primer administrador global (RF-078) ---
        services.AddScoped<BootstrapAdministradorService>();
        services.AddHostedService<BootstrapHostedService>();

        // --- Casos de uso de Aplicación (US3: catálogos maestros) ---
        // El servicio es genérico abierto: registrarlo así lo resuelve para cualquier catálogo sin
        // enumerarlos uno a uno, y añadir uno nuevo no obliga a tocar esta composición.
        services.AddScoped<IMaestroMetadatos, MaestroMetadatos>();
        services.AddScoped(typeof(MasterDataService<>));

        // --- Casos de uso de Aplicación (US2) ---
        services.AddScoped<DependenciasTipoCompaniaValidator>();
        services.AddScoped<CompaniaService>();
        services.AddScoped<RelacionContratistaPrincipalService>();
        services.AddScoped<UnidadOrganizativaService>();

        // --- Casos de uso de Aplicación (US4) ---
        services.AddScoped<PersonaService>();

        // --- Casos de uso de Aplicación (US5: históricos, contexto operativo y revocación) ---
        services.AddScoped<ContencionTemporalValidator>();
        services.AddScoped<RevocacionService>();
        services.AddScoped<HistorialPersonaService>();
        services.AddScoped<ContextoOperativoService>();
        services.AddScoped<AsignacionUnidadOrganizativaService>();
        services.AddScoped<EstadoEfectivoService>();
        services.AddScoped<CredencialService>();

        // --- Casos de uso de Aplicación (US6 y US7) ---
        services.AddScoped<AreaAccesoService>();

        // --- Casos de uso de Aplicación (US8: permisos y evaluación de acceso) ---
        // El evaluador es un servicio de dominio puro: se registra porque necesita el reloj
        // empresarial, no porque dependa de la infraestructura (research.md §18).
        services.AddScoped<EvaluadorDeAcceso>();
        services.AddScoped<PermisoAccesoService>();
        services.AddScoped<EvaluacionAccesoService>();

        // --- Autorización de plataforma (distinta del motor de dominio, research.md §18) ---
        services.AddScoped<IAuthorizationHandler, CompaniaScopeAuthorizationHandler>();

        return services;
    }
}
