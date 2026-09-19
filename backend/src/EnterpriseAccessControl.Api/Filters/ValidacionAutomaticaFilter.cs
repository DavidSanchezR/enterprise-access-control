using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EnterpriseAccessControl.Api.Filters;

/// <summary>
/// Ejecuta el <see cref="IValidator{T}"/> registrado para cada argumento de la acción antes de
/// invocarla.
/// </summary>
/// <remarks>
/// Sustituye a la auto-validación de FluentValidation.AspNetCore, paquete descontinuado y construido
/// para la versión mayor anterior de FluentValidation.
///
/// Al lanzar <see cref="ValidationException"/> reutiliza el único punto de traducción de errores del
/// sistema (<c>ExceptionHandlingMiddleware</c>), que ya produce <c>ValidationProblemDetails</c>
/// conforme a RFC 9457; así una violación detectada en el borde y otra detectada dentro de un servicio
/// de aplicación devuelven exactamente la misma forma de respuesta.
/// </remarks>
public sealed class ValidacionAutomaticaFilter(IServiceProvider proveedor) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        List<ValidationFailure>? fallos = null;

        foreach (var argumento in context.ActionArguments.Values)
        {
            if (argumento is null)
            {
                continue;
            }

            var validador = proveedor.GetService(typeof(IValidator<>).MakeGenericType(argumento.GetType()));
            if (validador is not IValidator instancia)
            {
                continue;
            }

            var contexto = new ValidationContext<object>(argumento);
            var resultado = await instancia.ValidateAsync(contexto, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (!resultado.IsValid)
            {
                (fallos ??= []).AddRange(resultado.Errors);
            }
        }

        if (fallos is not null)
        {
            throw new ValidationException(fallos);
        }

        await next().ConfigureAwait(false);
    }
}
