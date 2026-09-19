namespace EnterpriseAccessControl.Application.Common;

/// <summary>
/// Página de resultados, con la forma que declaran los contratos (<c>items</c>, <c>total</c>,
/// <c>pagina</c>, <c>tamañoPagina</c>).
/// </summary>
public sealed record PaginaResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Pagina,
    int TamañoPagina);

/// <summary>Parámetros de paginación normalizados y acotados.</summary>
public sealed record ParametrosPaginacion
{
    private const int TamañoMaximo = 200;
    private const int TamañoPorDefecto = 20;

    public ParametrosPaginacion(int? pagina, int? tamañoPagina)
    {
        // Se normaliza en lugar de rechazar: un tamaño fuera de rango es un detalle de cliente, no
        // un error de negocio. El tope evita que una consulta abierta degrade CS-002.
        Pagina = pagina is null or < 1 ? 1 : pagina.Value;
        TamañoPagina = tamañoPagina switch
        {
            null or < 1 => TamañoPorDefecto,
            > TamañoMaximo => TamañoMaximo,
            _ => tamañoPagina.Value,
        };
    }

    public int Pagina { get; }

    public int TamañoPagina { get; }

    public int Saltar => (Pagina - 1) * TamañoPagina;
}
