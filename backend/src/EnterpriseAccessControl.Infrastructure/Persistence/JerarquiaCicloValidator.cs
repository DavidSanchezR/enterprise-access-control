using EnterpriseAccessControl.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Infrastructure.Persistence;

/// <summary>
/// Recorrido de jerarquías padre-hijo mediante CTE recursivo de SQL Server (research.md §4).
/// </summary>
/// <remarks>
/// Cubre las dos necesidades que comparten <c>UnidadOrganizativa</c> (US2) y <c>ÁreaAcceso</c>
/// (US6):
/// <list type="number">
///   <item>Detección de ciclos antes de confirmar una creación o reubicación (RF-038, Principio V).</item>
///   <item>Resolución del nodo raíz de un nodo dado — necesaria para <c>UnidadOrganizativa</c>,
///   que por restricción de negocio no tiene FK hacia <c>Compañía</c> (RF-044) y resuelve su
///   Compañía Principal propietaria subiendo hasta la raíz y consultando
///   <c>CompañíaPrincipalUnidadOrganizativaRaiz</c> (RF-045).</item>
/// </list>
/// Se usa SQL crudo con CTE recursivo en lugar de cargar la jerarquía en memoria porque el árbol
/// puede ser profundo y la comprobación ocurre en cada movimiento de nodo; el recorrido debe
/// resolverse en una sola ida a la base de datos.
/// </remarks>
public sealed class JerarquiaCicloValidator(AppDbContext db) : IJerarquiaConsultas
{
    /// <summary>
    /// Indica si asignar <paramref name="nuevoPadreId"/> como padre de <paramref name="nodoId"/>
    /// crearía un ciclo, es decir, si el nodo es ancestro (directo o indirecto) del nuevo padre.
    /// </summary>
    public async Task<bool> CrearíaCicloAsync(
        string tabla,
        string columnaPadre,
        Guid nodoId,
        Guid nuevoPadreId,
        CancellationToken cancellationToken = default)
    {
        // Un nodo nunca puede ser su propio padre: es el ciclo más corto posible y no requiere
        // consultar la base de datos.
        if (nodoId == nuevoPadreId)
        {
            return true;
        }

        var ancestros = await ObtenerAncestrosAsync(tabla, columnaPadre, nuevoPadreId, cancellationToken)
            .ConfigureAwait(false);

        // Si el nodo que se está moviendo aparece entre los ancestros del destino, moverlo allí
        // cerraría el ciclo.
        return ancestros.Contains(nodoId);
    }

    /// <summary>Devuelve los ancestros del nodo indicado, del más cercano a la raíz.</summary>
    public async Task<IReadOnlyList<Guid>> ObtenerAncestrosAsync(
        string tabla,
        string columnaPadre,
        Guid nodoId,
        CancellationToken cancellationToken = default)
    {
        var sql = ConstruirSqlAncestros(tabla, columnaPadre);

        var ids = await db.Database
            .SqlQueryRaw<Guid>(sql, nodoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids;
    }

    /// <summary>Devuelve el identificador del nodo raíz del árbol al que pertenece el nodo indicado.</summary>
    public async Task<Guid> ObtenerRaizAsync(
        string tabla,
        string columnaPadre,
        Guid nodoId,
        CancellationToken cancellationToken = default)
    {
        var ancestros = await ObtenerAncestrosAsync(tabla, columnaPadre, nodoId, cancellationToken)
            .ConfigureAwait(false);

        // Sin ancestros, el propio nodo es la raíz.
        return ancestros.Count == 0 ? nodoId : ancestros[^1];
    }

    private static string ConstruirSqlAncestros(string tabla, string columnaPadre)
    {
        // Los nombres de tabla/columna provienen exclusivamente de constantes internas del código
        // (nunca de entrada del usuario) y se validan aquí como identificadores simples antes de
        // interpolarse, porque un identificador no puede parametrizarse en T-SQL.
        ValidarIdentificador(tabla);
        ValidarIdentificador(columnaPadre);

        // Prefijo $$ para que {0} —el marcador posicional de parámetro de EF Core— quede literal y
        // solo {{...}} se interprete como interpolación.
        return $$"""
            WITH Ancestros AS (
                SELECT t.[Id], t.[{{columnaPadre}}], 0 AS Nivel
                FROM [{{tabla}}] AS t
                WHERE t.[Id] = {0}

                UNION ALL

                SELECT p.[Id], p.[{{columnaPadre}}], a.Nivel + 1
                FROM [{{tabla}}] AS p
                INNER JOIN Ancestros AS a ON p.[Id] = a.[{{columnaPadre}}]
            )
            SELECT a.[Id] AS [Value]
            FROM Ancestros AS a
            WHERE a.Nivel > 0
            ORDER BY a.Nivel
            """;
    }

    private static void ValidarIdentificador(string identificador)
    {
        if (string.IsNullOrWhiteSpace(identificador)
            || !identificador.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException(
                $"Identificador SQL no válido: '{identificador}'.",
                nameof(identificador));
        }
    }
}
