namespace EnterpriseAccessControl.Application.Common.Errores;

/// <summary>
/// Error de negocio con un código estable y legible por máquina (research.md §21).
/// </summary>
/// <remarks>
/// El <see cref="Codigo"/> viaja en la extensión <c>codigo</c> de ProblemDetails para que el
/// frontend distinga casos sin analizar el texto de <c>detail</c>, que es para humanos y puede
/// cambiar de redacción.
/// </remarks>
public class ErrorNegocioException(string codigo, string mensaje, int statusCode)
    : Exception(mensaje)
{
    public string Codigo { get; } = codigo;

    public int StatusCode { get; } = statusCode;
}

/// <summary>Violación de una regla de validación de entrada o de estado (400).</summary>
public sealed class ReglaNegocioInvalidaException(string codigo, string mensaje)
    : ErrorNegocioException(codigo, mensaje, 400);

/// <summary>
/// Conflicto con el estado actual de los datos: solapamiento de vigencia, contención temporal
/// violada, estado incompatible (409).
/// </summary>
public sealed class ConflictoEstadoException(string codigo, string mensaje)
    : ErrorNegocioException(codigo, mensaje, 409);

/// <summary>
/// Recurso inexistente o fuera del alcance de compañías del usuario (404).
/// </summary>
/// <remarks>
/// Se responde 404 —y no 403— también cuando el recurso existe pero está fuera del alcance: revelar
/// la diferencia permitiría enumerar recursos ajenos conociendo su identificador (Principio I,
/// RF-005).
/// </remarks>
public sealed class RecursoNoEncontradoException(string codigo, string mensaje)
    : ErrorNegocioException(codigo, mensaje, 404);
