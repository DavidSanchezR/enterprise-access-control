using System.Text;
using System.Text.Json.Serialization;
using EnterpriseAccessControl.Api.Filters;
using EnterpriseAccessControl.Api.Middleware;
using EnterpriseAccessControl.Application.Auth.Validators;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Infrastructure;
using EnterpriseAccessControl.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options => options.Filters.Add<ValidacionAutomaticaFilter>())
    .AddJsonOptions(options =>
    {
        // Los enumerados viajan como su literal de texto (ACTIVO, PRINCIPAL_MANDANTE…), que es
        // exactamente el valor persistido en nvarchar y el declarado en los contratos. Con la
        // serialización numérica por defecto, el contrato ("enum: [ACTIVO, …]") y la respuesta real
        // (0, 1, 2) divergirían, y cualquier reordenamiento de miembros cambiaría en silencio el
        // significado de los datos ya emitidos.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// El generador de OpenAPI de .NET 10 deriva los esquemas de las opciones de Http.Json, no de las de
// MVC: sin esta segunda configuración el documento declararía los enumerados como enteros aunque las
// respuestas reales viajen como texto, que es justo la divergencia que las pruebas de contrato vigilan.
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Validadores FluentValidation de la capa de Aplicación, ejecutados por ValidacionAutomaticaFilter.
builder.Services.AddValidatorsFromAssemblyContaining<CrearUsuarioRequestValidator>();

// OpenAPI nativo de .NET 10 (research.md §10) — NO Swashbuckle.
builder.Services.AddOpenApi();

// ProblemDetails RFC 7807/9457 como único formato de error del sistema (research.md §21).
builder.Services.AddProblemDetails();

builder.Services.AddInfrastructure(builder.Configuration);

// --- Autenticación JWT Bearer (research.md §2, §18) ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Falta la sección de configuración '{JwtOptions.SectionName}'.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            // Sin tolerancia de reloj: la expiración del token es exacta, coherente con un dominio
            // donde la vigencia temporal se evalúa al instante (Principio IV).
            ClockSkew = TimeSpan.Zero,
        };
    });

// Política de alcance administrativo por compañías (RF-005, RF-049, RF-060).
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        CompaniaScopeRequirement.PolicyName,
        policy => policy.AddRequirements(new CompaniaScopeRequirement()))
    // Denegación por defecto a nivel de plataforma (Principio I): un endpoint que olvide declarar
    // su autorización queda protegido igualmente, en lugar de publicarse anónimo. Las únicas
    // excepciones se declaran explícitamente con AllowAnonymous (login, health checks, OpenAPI).
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// --- Health checks (research.md §19) ---
var connectionString = builder.Configuration.GetConnectionString("SqlServer");

var healthChecks = builder.Services.AddHealthChecks();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    // La cadena se resuelve al ejecutar la comprobación y no al registrarla: así /health/ready sondea
    // exactamente la misma base de datos que usa el DbContext, incluso si una fuente de configuración
    // se incorpora después de este punto (variables de entorno del contenedor, proveedores de secretos,
    // o la configuración de las pruebas de integración).
    healthChecks.AddSqlServer(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("SqlServer")!,
        name: "sqlserver",
        tags: ["ready"]);
}

var app = builder.Build();

// El manejo de errores va primero para capturar todo lo que ocurra aguas abajo.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Solo en Development, y explícitamente anónimo: la política de respaldo lo protegería si no.
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness: responde sin tocar dependencias externas — el proceso está vivo.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

// Readiness: incluye conectividad con SQL Server — la API puede atender tráfico real.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();

await app.RunAsync();

/// <summary>
/// Expuesto para que <c>WebApplicationFactory&lt;Program&gt;</c> pueda arrancar la API en las
/// pruebas de integración y de contrato.
/// </summary>
public partial class Program;
