namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Recorrido de jerarquías padre-hijo, resuelto en la base de datos (research.md §4).
/// </summary>
/// <remarks>
/// La Aplicación necesita dos respuestas que sólo el motor puede dar de una sola vez sobre un árbol
/// de profundidad arbitraria: si una reubicación crearía un ciclo (RF-038, Principio V) y cuál es la
/// raíz de un nodo —de la que <c>UnidadOrganizativa</c> deriva su Compañía Principal, al no tener FK
/// hacia <c>Compañía</c> (RF-044, RF-045)—.
///
/// Se declara como puerto para que los casos de uso no dependan del CTE recursivo de SQL Server que
/// lo implementa.
/// </remarks>
public interface IJerarquiaConsultas
{
    /// <summary>Indica si hacer de <paramref name="nuevoPadreId"/> el padre de <paramref name="nodoId"/> crearía un ciclo.</summary>
    Task<bool> CrearíaCicloAsync(
        string tabla,
        string columnaPadre,
        Guid nodoId,
        Guid nuevoPadreId,
        CancellationToken cancellationToken = default);

    /// <summary>Ancestros del nodo, del más cercano a la raíz.</summary>
    Task<IReadOnlyList<Guid>> ObtenerAncestrosAsync(
        string tabla,
        string columnaPadre,
        Guid nodoId,
        CancellationToken cancellationToken = default);

    /// <summary>Raíz del árbol al que pertenece el nodo; el propio nodo si ya es raíz.</summary>
    Task<Guid> ObtenerRaizAsync(
        string tabla,
        string columnaPadre,
        Guid nodoId,
        CancellationToken cancellationToken = default);
}

/// <summary>Nombres de tabla y columna de las jerarquías del modelo, en un único sitio.</summary>
/// <remarks>
/// Son identificadores SQL que se interpolan en el CTE recursivo —un identificador no puede
/// parametrizarse en T-SQL—, de modo que deben provenir siempre de constantes del código y nunca de
/// entrada del usuario.
/// </remarks>
public static class Jerarquias
{
    public const string TablaUnidadOrganizativa = "UnidadOrganizativa";

    public const string ColumnaPadreUnidadOrganizativa = "UnidadSuperiorId";
}
