using EnterpriseAccessControl.Application.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseAccessControl.Infrastructure.Hosting;

/// <summary>
/// Ejecuta la siembra del primer administrador global al arrancar la aplicación (RF-078).
/// </summary>
/// <remarks>
/// Se implementa como <see cref="IHostedService"/> y no como migración de datos porque necesita leer
/// configuración y secretos, algo que una migración de EF Core no hace de forma natural
/// (research.md §28).
///
/// Se omite cuando no hay cadena de conexión configurada: es el mismo criterio que ya aplica
/// <c>Program.cs</c> al health check de SQL Server, y permite que las pruebas de contrato —que
/// levantan la API sin base de datos para leer solo el documento OpenAPI— sigan arrancando.
/// </remarks>
public sealed partial class BootstrapHostedService(
    IServiceProvider servicios,
    IConfiguration configuration,
    ILogger<BootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("SqlServer")))
        {
            SiembraOmitida(logger);
            return;
        }

        using var ambito = servicios.CreateScope();

        var bootstrap = ambito.ServiceProvider.GetRequiredService<BootstrapAdministradorService>();

        var resultado = await bootstrap.EjecutarAsync(cancellationToken).ConfigureAwait(false);

        // Se registra el resultado, nunca la contraseña ni su longitud.
        SiembraEjecutada(logger, resultado);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Arranque sin cadena de conexión: se omite la siembra del administrador inicial.")]
    private static partial void SiembraOmitida(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Siembra del administrador inicial: {Resultado}.")]
    private static partial void SiembraEjecutada(ILogger logger, ResultadoBootstrap resultado);
}
