using EnterpriseAccessControl.Application.Common.Errores;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Api.Middleware;

/// <summary>
/// Traduce excepciones de dominio/infraestructura a respuestas ProblemDetails RFC 7807/9457
/// (research.md §21).
/// </summary>
/// <remarks>
/// Centralizar la traducción evita que cada controlador construya respuestas de error por su
/// cuenta, lo que garantiza un único formato de error en toda la API — requisito explícito del
/// stack ratificado, que reemplazó el envelope propio <c>ErrorResponse</c> de los contratos.
///
/// También asegura que nunca se filtren detalles internos (stack traces, SQL) al cliente
/// (Constitución: "Las respuestas de API NO DEBEN exponer datos internos innecesarios").
/// </remarks>
public sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Tipo de contenido exigido por RFC 9457 para una respuesta de problema.</summary>
    private const string TipoContenidoProblema = "application/problem+json";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            // FluentValidation: se expone como ValidationProblemDetails con los errores por campo.
            LogValidacionRechazada(logger, ex);
            await EscribirValidacionAsync(context, ex);
        }
        catch (ErrorNegocioException ex)
        {
            LogReglaNegocioRechazada(logger, ex.Codigo, ex);
            await EscribirProblemDetailsAsync(context, ex.StatusCode, ex.Codigo, ex.Message);
        }
        catch (DbUpdateException ex) when (EsSolapamientoDeTrigger(ex))
        {
            LogSolapamientoDetectadoPorTrigger(logger, ex);

            // Los triggers de no-solapamiento son la última línea de defensa ante una condición de
            // carrera (research.md §5). Sin esta traducción, una carrera genuina saldría como 500
            // —un fallo de infraestructura— cuando en realidad es un conflicto de negocio que el
            // cliente puede entender y reintentar.
            await EscribirProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                CodigosError.SolapamientoVigencia,
                "La operación se cruza con otra vigencia del mismo ámbito. Vuelva a cargar los datos e intente de nuevo.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            LogConflictoConcurrencia(logger, ex);

            await EscribirProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                CodigosError.ConflictoConcurrencia,
                "El registro fue modificado por otro usuario. Vuelva a cargarlo e intente de nuevo.");
        }
        catch (Exception ex)
        {
            LogErrorNoControlado(logger, ex);

            await EscribirProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error inesperado al procesar la solicitud.");
        }
    }

    // Logging con el generador de origen LoggerMessage: evita evaluar y asignar argumentos cuando
    // el nivel está deshabilitado.
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Validación de entrada rechazada.")]
    private static partial void LogValidacionRechazada(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Regla de negocio rechazada. Codigo={codigo}")]
    private static partial void LogReglaNegocioRechazada(ILogger logger, string codigo, Exception ex);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Conflicto de concurrencia optimista (rowversion).")]
    private static partial void LogConflictoConcurrencia(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Error no controlado.")]
    private static partial void LogErrorNoControlado(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Solapamiento de vigencia rechazado por la base de datos (condición de carrera).")]
    private static partial void LogSolapamientoDetectadoPorTrigger(ILogger logger, Exception ex);

    /// <summary>
    /// Identifica el error que lanzan los triggers de no-solapamiento.
    /// </summary>
    /// <remarks>
    /// 50001 es el número que usan todos ellos en su <c>THROW</c>. Se compara por número y no por
    /// texto del mensaje, que depende del idioma del servidor y podría cambiar.
    /// </remarks>
    private static bool EsSolapamientoDeTrigger(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number == 50001;

    private static async Task EscribirProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string codigo,
        string detalle)
    {
        if (context.Response.HasStarted)
        {
            // La respuesta ya se está enviando: reescribirla produciría una respuesta corrupta.
            return;
        }

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9457",
            Title = TituloPara(statusCode),
            Status = statusCode,
            Detail = detalle,
            Instance = context.Request.Path,
        };

        problem.Extensions["codigo"] = codigo;

        context.Response.StatusCode = statusCode;

        // El tipo de contenido se pasa a WriteAsJsonAsync y no se asigna a Response.ContentType:
        // WriteAsJsonAsync sobrescribe la cabecera con application/json, y RFC 9457 exige
        // application/problem+json para que el cliente reconozca la respuesta como un problema.
        await context.Response
            .WriteAsJsonAsync(problem, options: null, TipoContenidoProblema, context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static async Task EscribirValidacionAsync(HttpContext context, ValidationException ex)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var errores = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errores)
        {
            Type = "https://tools.ietf.org/html/rfc9457",
            Title = "Solicitud inválida",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path,
        };

        problem.Extensions["codigo"] = CodigosError.ValidacionEntrada;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        await context.Response
            .WriteAsJsonAsync(problem, options: null, TipoContenidoProblema, context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static string TituloPara(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Solicitud inválida",
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status409Conflict => "Conflicto con el estado actual",
        _ => "Error",
    };
}
