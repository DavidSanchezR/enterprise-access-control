using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Companies;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.AreaAccess;

/// <summary>
/// Árbol de áreas físicas de acceso por Compañía Principal
/// (Historia 6, RF-009, RF-038, RF-046, RF-049).
/// </summary>
/// <remarks>
/// Cada área pertenece a exactamente una Compañía Principal, y esa pertenencia se guarda en el propio
/// nodo (RF-046) —a diferencia de <c>UnidadOrganizativa</c>, que la resuelve subiendo hasta la raíz
/// (RF-044)—.
///
/// Que el dato esté duplicado en cada nodo obliga a mantenerlo coherente: el área hija lo copia de su
/// padre al crearse y no puede editarse por separado, y mover un área a otro árbol se rechaza en lugar
/// de reescribir el identificador. Sin esa disciplina, dos nodos del mismo árbol podrían acabar
/// declarando Principales distintas.
/// </remarks>
public sealed class AreaAccesoService(
    IAppDbContext db,
    CompaniaService companias,
    IJerarquiaConsultas jerarquia)
{
    /// <summary>Identificadores SQL de esta jerarquía, para el CTE recursivo de detección de ciclos.</summary>
    private const string Tabla = "AreaAcceso";

    private const string ColumnaPadre = "AreaSuperiorId";

    public async Task<IReadOnlyList<AreaAccesoDto>> ListarAsync(
        Guid companiaPrincipalId,
        Estado? estado,
        CancellationToken ct = default)
    {
        await ExigirPrincipalEnAlcanceAsync(companiaPrincipalId, ct).ConfigureAwait(false);

        var consulta = db.AreasAcceso
            .AsNoTracking()
            .Where(a => a.CompaniaPrincipalId == companiaPrincipalId);

        if (estado is not null)
        {
            consulta = consulta.Where(a => a.Estado == estado);
        }

        return await consulta
            .OrderBy(a => a.Nombre)
            .Select(a => new AreaAccesoDto(
                a.Id, a.Nombre, a.AreaSuperiorId, a.CompaniaPrincipalId, a.Estado))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NodoAreaDto>> ObtenerArbolAsync(
        Guid companiaPrincipalId,
        CancellationToken ct = default)
    {
        await ExigirPrincipalEnAlcanceAsync(companiaPrincipalId, ct).ConfigureAwait(false);

        // El árbol completo de una Principal se lee de una vez: recorrerlo por niveles multiplicaría
        // las idas a la base de datos sin ganar nada, porque el conjunto ya está acotado por la FK.
        var areas = await db.AreasAcceso
            .AsNoTracking()
            .Where(a => a.CompaniaPrincipalId == companiaPrincipalId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (areas.Count == 0)
        {
            return [];
        }

        var porPadre = areas
            .Where(a => a.AreaSuperiorId is not null)
            .GroupBy(a => a.AreaSuperiorId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return [.. areas
            .Where(a => a.AreaSuperiorId is null)
            .OrderBy(a => a.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(raiz => ConstruirNodo(raiz, porPadre))];
    }

    public async Task<AreaAccesoDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var area = await ObtenerEnAlcanceAsync(id, seguimiento: false, ct).ConfigureAwait(false);

        return AMapa(area);
    }

    public async Task<AreaAccesoDto> CrearAsync(
        AreaAccesoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var principalId = request.AreaSuperiorId is null
            ? await ResolverPrincipalDeRaizAsync(request, ct).ConfigureAwait(false)
            : await ResolverPrincipalDelPadreAsync(request.AreaSuperiorId.Value, ct)
                .ConfigureAwait(false);

        var area = new AreaAcceso
        {
            Nombre = request.Nombre.Trim(),
            AreaSuperiorId = request.AreaSuperiorId,
            CompaniaPrincipalId = principalId,
            Estado = request.Estado,
        };

        db.AreasAcceso.Add(area);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(area);
    }

    public async Task<AreaAccesoDto> ActualizarAsync(
        Guid id,
        AreaAccesoUpdateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var area = await ObtenerEnAlcanceAsync(id, seguimiento: true, ct).ConfigureAwait(false);

        // La actualización no toca ni la jerarquía ni la Principal propietaria: reubicar es
        // POST /mover, y la pertenencia no es editable (contracts/area-access.yaml).
        area.Nombre = request.Nombre.Trim();
        area.Estado = request.Estado;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(area);
    }

    public async Task MoverAsync(Guid id, MoverAreaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var area = await ObtenerEnAlcanceAsync(id, seguimiento: true, ct).ConfigureAwait(false);

        if (request.NuevoPadreId is null)
        {
            // Convertir en raíz conserva la Principal: el área no cambia de dueño, solo de posición.
            area.AreaSuperiorId = null;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var nuevoPadre = await db.AreasAcceso
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.NuevoPadreId.Value, ct)
            .ConfigureAwait(false)
            ?? throw new ConflictoEstadoException(
                CodigosError.RecursoNoEncontrado, "El área superior indicada no existe.");

        // Un área no puede cambiar de árbol: eso alteraría su Compañía Principal propietaria
        // (RF-046). Se rechaza en lugar de reescribir el identificador en el nodo y su subárbol.
        if (nuevoPadre.CompaniaPrincipalId != area.CompaniaPrincipalId)
        {
            throw new ConflictoEstadoException(
                CodigosError.CompaniaDebeSerPrincipal,
                "No se puede reubicar el área bajo un padre del árbol de otra Compañía Principal.");
        }

        var crearíaCiclo = await jerarquia
            .CrearíaCicloAsync(Tabla, ColumnaPadre, area.Id, nuevoPadre.Id, ct)
            .ConfigureAwait(false);

        if (crearíaCiclo)
        {
            throw new ConflictoEstadoException(
                CodigosError.CicloJerarquico,
                "La reubicación crearía un ciclo en la jerarquía de áreas (RF-038).");
        }

        area.AreaSuperiorId = nuevoPadre.Id;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // --- Tipos de persona autorizados por área (Historia 7, RF-019) ---------------------------

    /// <summary>Tipos de persona que el área admite (RF-019).</summary>
    public async Task<IReadOnlyList<Guid>> ObtenerTiposPersonaAsync(
        Guid areaId,
        CancellationToken ct = default)
    {
        // Pasa por la misma resolución en alcance que el resto: el área de otra Principal debe ser
        // indistinguible de inexistente también aquí (RF-049).
        await ObtenerEnAlcanceAsync(areaId, seguimiento: false, ct).ConfigureAwait(false);

        return await db.AreasAccesoTipoPersona
            .AsNoTracking()
            .Where(t => t.AreaAccesoId == areaId)
            .Select(t => t.TipoPersonaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>Reemplaza el conjunto completo de tipos de persona autorizados (RF-019).</summary>
    public async Task ReemplazarTiposPersonaAsync(
        Guid areaId,
        ReemplazarTiposPersonaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ObtenerEnAlcanceAsync(areaId, seguimiento: false, ct).ConfigureAwait(false);

        var ids = request.TipoPersonaIds.Distinct().ToList();

        await ExigirTiposPersonaActivosAsync(ids, ct).ConfigureAwait(false);

        var actuales = await db.AreasAccesoTipoPersona
            .Where(t => t.AreaAccesoId == areaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Se conservan las filas que siguen estando: reinsertarlas cambiaría su auditoría y haría
        // parecer que la autorización es nueva cuando en realidad no se tocó (Principio III).
        var aRetirar = actuales.Where(t => !ids.Contains(t.TipoPersonaId)).ToList();
        var yaPresentes = actuales.Select(t => t.TipoPersonaId).ToHashSet();

        db.AreasAccesoTipoPersona.RemoveRange(aRetirar);

        foreach (var tipoPersonaId in ids.Where(id => !yaPresentes.Contains(id)))
        {
            db.AreasAccesoTipoPersona.Add(new AreaAccesoTipoPersona
            {
                AreaAccesoId = areaId,
                TipoPersonaId = tipoPersonaId,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Un tipo de persona inexistente o INACTIVO no puede autorizarse en un área (RF-032).
    /// </summary>
    private async Task ExigirTiposPersonaActivosAsync(
        List<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var validos = await db.Maestro<TipoPersona>()
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id) && t.Estado == Estado.ACTIVO)
            .Select(t => t.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var invalidos = ids.Except(validos).ToList();

        if (invalidos.Count > 0)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValorMaestroInactivo,
                $"Tipos de persona inexistentes o inactivos: {string.Join(", ", invalidos)}.");
        }
    }

    // --- Resolución de pertenencia ------------------------------------------------------------

    /// <summary>Un área raíz exige una Principal explícita y válida (RF-046).</summary>
    private async Task<Guid> ResolverPrincipalDeRaizAsync(
        AreaAccesoRequest request,
        CancellationToken ct)
    {
        if (request.CompaniaPrincipalId is null)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                "Un área raíz requiere companiaPrincipalId (RF-046).");
        }

        // Debe existir, estar en alcance y ser PRINCIPAL_MANDANTE: una CONTRATISTA no posee áreas.
        var principal = await companias
            .ObtenerDeTipoAsync(request.CompaniaPrincipalId.Value, TipoCompania.PRINCIPAL_MANDANTE, ct)
            .ConfigureAwait(false);

        return principal.Id;
    }

    /// <summary>Un área hija copia la Principal de su padre; el valor enviado se ignora.</summary>
    private async Task<Guid> ResolverPrincipalDelPadreAsync(Guid areaSuperiorId, CancellationToken ct)
    {
        var padre = await db.AreasAcceso
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == areaSuperiorId, ct)
            .ConfigureAwait(false)
            ?? throw new ConflictoEstadoException(
                CodigosError.RecursoNoEncontrado, "El área superior indicada no existe.");

        await ExigirPrincipalEnAlcanceAsync(padre.CompaniaPrincipalId, ct).ConfigureAwait(false);

        return padre.CompaniaPrincipalId;
    }

    /// <summary>
    /// Recupera un área exigiendo que su Compañía Principal esté dentro del alcance (RF-049).
    /// </summary>
    private async Task<AreaAcceso> ObtenerEnAlcanceAsync(
        Guid id,
        bool seguimiento,
        CancellationToken ct)
    {
        var consulta = seguimiento ? db.AreasAcceso : db.AreasAcceso.AsNoTracking();

        var area = await consulta.FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false)
            ?? throw NoEncontrada();

        // Fuera de alcance debe ser indistinguible de inexistente (Principio I).
        await companias
            .ObtenerEnAlcanceAsync(area.CompaniaPrincipalId, seguimiento: false, ct)
            .ConfigureAwait(false);

        return area;
    }

    /// <summary>
    /// Exige que la compañía indicada esté en alcance y sea PRINCIPAL_MANDANTE.
    /// </summary>
    /// <remarks>
    /// Una CONTRATISTA devuelve 404 y no 400 al consultar: sencillamente no tiene árbol de áreas que
    /// mostrar (contracts/area-access.yaml).
    /// </remarks>
    private async Task ExigirPrincipalEnAlcanceAsync(Guid companiaPrincipalId, CancellationToken ct)
    {
        var compania = await companias
            .ObtenerEnAlcanceAsync(companiaPrincipalId, seguimiento: false, ct)
            .ConfigureAwait(false);

        if (compania.TipoCompania != TipoCompania.PRINCIPAL_MANDANTE)
        {
            throw NoEncontrada();
        }
    }

    private static NodoAreaDto ConstruirNodo(
        AreaAcceso nodo,
        Dictionary<Guid, List<AreaAcceso>> porPadre) =>
        new(
            nodo.Id,
            nodo.Nombre,
            nodo.Estado,
            porPadre.TryGetValue(nodo.Id, out var hijos)
                ? [.. hijos
                    .OrderBy(h => h.Nombre, StringComparer.OrdinalIgnoreCase)
                    .Select(h => ConstruirNodo(h, porPadre))]
                : []);

    private static AreaAccesoDto AMapa(AreaAcceso a) =>
        new(a.Id, a.Nombre, a.AreaSuperiorId, a.CompaniaPrincipalId, a.Estado);

    private static RecursoNoEncontradoException NoEncontrada() =>
        new(CodigosError.RecursoNoEncontrado, "Área de acceso no encontrada.");
}
