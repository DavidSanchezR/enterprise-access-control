using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.OrgUnits;

/// <summary>
/// Jerarquía de unidades organizativas de una Compañía Principal (RF-007, RF-008, RF-038, RF-043 a
/// RF-045; contracts/org-units.yaml).
/// </summary>
/// <remarks>
/// La entidad no guarda su compañía (RF-044). La pertenencia se resuelve siempre igual: subir hasta
/// la raíz del árbol y leer <c>CompañíaPrincipalUnidadOrganizativaRaiz</c>. Todas las operaciones
/// parten de una Compañía Principal explícita y en alcance, de modo que dos Principales nunca ven ni
/// tocan el árbol de la otra (RF-043, CS-011).
/// </remarks>
public sealed class UnidadOrganizativaService(
    IAppDbContext db,
    CompaniaService companias,
    IJerarquiaConsultas jerarquia)
{
    public async Task<IReadOnlyList<UnidadOrganizativaDto>> ListarAsync(
        Guid companiaPrincipalId,
        Estado? estado,
        CancellationToken ct = default)
    {
        var unidades = await ObtenerArbolPlanoAsync(companiaPrincipalId, ct).ConfigureAwait(false);

        return [.. unidades
            .Where(u => estado is null || u.Estado == estado)
            .OrderBy(u => u.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(u => AMapa(u, companiaPrincipalId))];
    }

    public async Task<IReadOnlyList<NodoArbolDto>> ObtenerArbolAsync(
        Guid companiaPrincipalId,
        CancellationToken ct = default)
    {
        var unidades = await ObtenerArbolPlanoAsync(companiaPrincipalId, ct).ConfigureAwait(false);

        if (unidades.Count == 0)
        {
            return [];
        }

        // El árbol se arma en memoria a partir de una única lectura: recorrer la jerarquía con una
        // consulta por nivel multiplicaría las idas a la base de datos sin ganar nada, dado que el
        // árbol completo de una Principal ya está acotado por su propia raíz.
        var porPadre = unidades
            .Where(u => u.UnidadSuperiorId is not null)
            .GroupBy(u => u.UnidadSuperiorId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var raices = unidades.Where(u => u.UnidadSuperiorId is null).ToList();

        return [.. raices
            .OrderBy(u => u.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(raiz => ConstruirNodo(raiz, porPadre))];
    }

    public async Task<UnidadOrganizativaDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var unidad = await db.UnidadesOrganizativas
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            .ConfigureAwait(false)
            ?? throw NoEncontrada();

        // La comprobación de alcance ocurre sobre la Principal propietaria, no sobre el nodo: el
        // nodo no conoce su compañía (RF-044).
        var principalId = await ResolverPrincipalAsync(unidad, ct).ConfigureAwait(false);
        await companias.ObtenerEnAlcanceAsync(principalId, seguimiento: false, ct).ConfigureAwait(false);

        return AMapa(unidad, principalId);
    }

    public async Task<UnidadOrganizativaDto> CrearAsync(
        UnidadOrganizativaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.UnidadSuperiorId is null
            ? await CrearRaizAsync(request, ct).ConfigureAwait(false)
            : await CrearHijaAsync(request, request.UnidadSuperiorId.Value, ct).ConfigureAwait(false);
    }

    public async Task<UnidadOrganizativaDto> ActualizarAsync(
        Guid id,
        UnidadOrganizativaUpdateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unidad = await db.UnidadesOrganizativas.FirstOrDefaultAsync(u => u.Id == id, ct)
            .ConfigureAwait(false)
            ?? throw NoEncontrada();

        var principalId = await ResolverPrincipalAsync(unidad, ct).ConfigureAwait(false);
        await companias.ObtenerEnAlcanceAsync(principalId, seguimiento: false, ct).ConfigureAwait(false);

        // La actualización no toca ni la jerarquía ni la compañía: reubicar es POST /mover, y la
        // Principal propietaria no es editable (contracts/org-units.yaml).
        unidad.Nombre = request.Nombre.Trim();
        unidad.Estado = request.Estado;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(unidad, principalId);
    }

    public async Task MoverAsync(Guid id, MoverUnidadRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unidad = await db.UnidadesOrganizativas.FirstOrDefaultAsync(u => u.Id == id, ct)
            .ConfigureAwait(false)
            ?? throw NoEncontrada();

        var principalId = await ResolverPrincipalAsync(unidad, ct).ConfigureAwait(false);
        await companias.ObtenerEnAlcanceAsync(principalId, seguimiento: false, ct).ConfigureAwait(false);

        if (request.NuevoPadreId is null)
        {
            await PromoverARaizAsync(unidad, principalId, ct).ConfigureAwait(false);
            return;
        }

        var nuevoPadre = await db.UnidadesOrganizativas
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.NuevoPadreId.Value, ct)
            .ConfigureAwait(false)
            ?? throw new ConflictoEstadoException(
                CodigosError.RecursoNoEncontrado,
                "La unidad superior indicada no existe.");

        // Un nodo no puede cambiar de árbol: eso alteraría implícitamente su Compañía Principal
        // propietaria (RF-045).
        var principalDestino = await ResolverPrincipalAsync(nuevoPadre, ct).ConfigureAwait(false);

        if (principalDestino != principalId)
        {
            throw new ConflictoEstadoException(
                CodigosError.CompaniaDebeSerPrincipal,
                "No se puede reubicar la unidad bajo un padre del árbol de otra Compañía Principal.");
        }

        await ValidarSinCicloAsync(unidad.Id, nuevoPadre.Id, ct).ConfigureAwait(false);

        var eraRaiz = unidad.UnidadSuperiorId is null;

        unidad.UnidadSuperiorId = nuevoPadre.Id;

        // Si el nodo dejó de ser raíz, su fila de enlace ya no describe una raíz y debe retirarse;
        // el árbol conserva la pertenencia a través de la raíz que lo absorbe.
        if (eraRaiz)
        {
            var enlace = await db.RaicesUnidadOrganizativa
                .FirstOrDefaultAsync(e => e.UnidadOrganizativaRaizId == unidad.Id, ct)
                .ConfigureAwait(false);

            if (enlace is not null)
            {
                db.RaicesUnidadOrganizativa.Remove(enlace);
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // --- Creación ---------------------------------------------------------------------------

    private async Task<UnidadOrganizativaDto> CrearRaizAsync(
        UnidadOrganizativaRequest request,
        CancellationToken ct)
    {
        if (request.CompaniaPrincipalId is null)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                "Un nodo raíz requiere companiaPrincipalId (RF-045).");
        }

        // Debe existir, estar en alcance y ser PRINCIPAL_MANDANTE: una CONTRATISTA no posee unidades
        // organizativas (RF-045, Historia 2 criterio 5).
        var principal = await companias
            .ObtenerDeTipoAsync(request.CompaniaPrincipalId.Value, TipoCompania.PRINCIPAL_MANDANTE, ct)
            .ConfigureAwait(false);

        var unidad = new UnidadOrganizativa
        {
            Nombre = request.Nombre.Trim(),
            UnidadSuperiorId = null,
            Estado = request.Estado,
        };

        db.UnidadesOrganizativas.Add(unidad);

        db.RaicesUnidadOrganizativa.Add(new CompaniaPrincipalUnidadOrganizativaRaiz
        {
            CompaniaId = principal.Id,
            UnidadOrganizativaRaizId = unidad.Id,
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(unidad, principal.Id);
    }

    private async Task<UnidadOrganizativaDto> CrearHijaAsync(
        UnidadOrganizativaRequest request,
        Guid unidadSuperiorId,
        CancellationToken ct)
    {
        var padre = await db.UnidadesOrganizativas
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unidadSuperiorId, ct)
            .ConfigureAwait(false)
            ?? throw new ConflictoEstadoException(
                CodigosError.RecursoNoEncontrado,
                "La unidad superior indicada no existe.");

        // El hijo hereda la Principal de su padre; companiaPrincipalId se ignora si viene informado
        // (contracts/org-units.yaml).
        var principalId = await ResolverPrincipalAsync(padre, ct).ConfigureAwait(false);
        await companias.ObtenerEnAlcanceAsync(principalId, seguimiento: false, ct).ConfigureAwait(false);

        var unidad = new UnidadOrganizativa
        {
            Nombre = request.Nombre.Trim(),
            UnidadSuperiorId = padre.Id,
            Estado = request.Estado,
        };

        db.UnidadesOrganizativas.Add(unidad);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(unidad, principalId);
    }

    private async Task PromoverARaizAsync(
        UnidadOrganizativa unidad,
        Guid principalId,
        CancellationToken ct)
    {
        if (unidad.UnidadSuperiorId is null)
        {
            // Ya es raíz: mover a raíz es una operación sin efecto, no un error.
            return;
        }

        unidad.UnidadSuperiorId = null;

        // Al quedar como raíz de su propio árbol necesita su fila de enlace para no perder la
        // pertenencia, que hasta ahora heredaba de su antiguo ancestro (contracts/org-units.yaml:
        // "companiaPrincipalId no cambia").
        db.RaicesUnidadOrganizativa.Add(new CompaniaPrincipalUnidadOrganizativaRaiz
        {
            CompaniaId = principalId,
            UnidadOrganizativaRaizId = unidad.Id,
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // --- Resolución de pertenencia y jerarquía ----------------------------------------------

    /// <summary>
    /// Resuelve la Compañía Principal propietaria del árbol al que pertenece el nodo (RF-045).
    /// </summary>
    private async Task<Guid> ResolverPrincipalAsync(UnidadOrganizativa unidad, CancellationToken ct)
    {
        var raizId = unidad.UnidadSuperiorId is null
            ? unidad.Id
            : await jerarquia
                .ObtenerRaizAsync(
                    Jerarquias.TablaUnidadOrganizativa,
                    Jerarquias.ColumnaPadreUnidadOrganizativa,
                    unidad.Id,
                    ct)
                .ConfigureAwait(false);

        var enlace = await db.RaicesUnidadOrganizativa
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UnidadOrganizativaRaizId == raizId, ct)
            .ConfigureAwait(false);

        // Un árbol sin fila de enlace sería un árbol huérfano, sin Principal propietaria: no debería
        // poder existir, porque toda raíz se crea junto a su enlace y mover a raíz lo repone.
        return enlace?.CompaniaId
            ?? throw new ConflictoEstadoException(
                CodigosError.RecursoNoEncontrado,
                "La unidad organizativa no tiene una Compañía Principal asociada.");
    }

    /// <summary>Lee el árbol completo de una Compañía Principal, validando alcance y tipo.</summary>
    private async Task<List<UnidadOrganizativa>> ObtenerArbolPlanoAsync(
        Guid companiaPrincipalId,
        CancellationToken ct)
    {
        var principal = await companias
            .ObtenerEnAlcanceAsync(companiaPrincipalId, seguimiento: false, ct)
            .ConfigureAwait(false);

        if (principal.TipoCompania != TipoCompania.PRINCIPAL_MANDANTE)
        {
            // contracts/org-units.yaml documenta 404 para este caso: una CONTRATISTA no tiene árbol
            // que consultar, así que el recurso sencillamente no existe.
            throw NoEncontrada();
        }

        var raices = await db.RaicesUnidadOrganizativa
            .AsNoTracking()
            .Where(e => e.CompaniaId == companiaPrincipalId)
            .Select(e => e.UnidadOrganizativaRaizId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (raices.Count == 0)
        {
            return [];
        }

        // Se traen todos los nodos y se filtran por descendencia de las raíces de esta Principal.
        // El aislamiento entre Principales se sostiene en este filtro, no en una columna del nodo.
        var todos = await db.UnidadesOrganizativas.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

        var porPadre = todos
            .Where(u => u.UnidadSuperiorId is not null)
            .GroupBy(u => u.UnidadSuperiorId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var resultado = new List<UnidadOrganizativa>();
        var porVisitar = new Stack<Guid>(raices);
        var visitados = new HashSet<Guid>();

        while (porVisitar.Count > 0)
        {
            var actual = porVisitar.Pop();

            // La guarda de visitados protege el recorrido frente a un ciclo que hubiera entrado en
            // los datos por otra vía: sin ella, este bucle no terminaría.
            if (!visitados.Add(actual))
            {
                continue;
            }

            var nodo = todos.Find(u => u.Id == actual);
            if (nodo is not null)
            {
                resultado.Add(nodo);
            }

            if (porPadre.TryGetValue(actual, out var hijos))
            {
                foreach (var hijo in hijos)
                {
                    porVisitar.Push(hijo.Id);
                }
            }
        }

        return resultado;
    }

    private async Task ValidarSinCicloAsync(Guid nodoId, Guid nuevoPadreId, CancellationToken ct)
    {
        var crearíaCiclo = await jerarquia
            .CrearíaCicloAsync(
                Jerarquias.TablaUnidadOrganizativa,
                Jerarquias.ColumnaPadreUnidadOrganizativa,
                nodoId,
                nuevoPadreId,
                ct)
            .ConfigureAwait(false);

        if (crearíaCiclo)
        {
            throw new ConflictoEstadoException(
                CodigosError.CicloJerarquico,
                "La reubicación crearía un ciclo en la jerarquía (RF-038).");
        }
    }

    private static NodoArbolDto ConstruirNodo(
        UnidadOrganizativa nodo,
        Dictionary<Guid, List<UnidadOrganizativa>> porPadre) =>
        new(
            nodo.Id,
            nodo.Nombre,
            nodo.Estado,
            porPadre.TryGetValue(nodo.Id, out var hijos)
                ? [.. hijos
                    .OrderBy(h => h.Nombre, StringComparer.OrdinalIgnoreCase)
                    .Select(h => ConstruirNodo(h, porPadre))]
                : []);

    private static UnidadOrganizativaDto AMapa(UnidadOrganizativa u, Guid companiaPrincipalId) =>
        new(u.Id, u.Nombre, u.UnidadSuperiorId, companiaPrincipalId, u.Estado);

    private static RecursoNoEncontradoException NoEncontrada() =>
        new(CodigosError.RecursoNoEncontrado, "Unidad organizativa no encontrada.");
}
