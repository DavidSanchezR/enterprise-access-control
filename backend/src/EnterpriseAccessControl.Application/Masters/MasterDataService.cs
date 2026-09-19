using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Masters;

/// <summary>contracts/masters.yaml — MasterItem.</summary>
public sealed record MasterItem(Guid Id, string Nombre, Estado Estado);

/// <summary>contracts/masters.yaml — MasterItemRequest.</summary>
public sealed record MasterItemRequest(string Nombre, Estado Estado);

/// <summary>
/// Mantenimiento de un catálogo maestro cualquiera (Historia 3, RF-030 a RF-032).
/// </summary>
/// <remarks>
/// Es genérico porque los cinco catálogos tienen forma y ciclo de vida idénticos: escribir cinco
/// servicios equivalentes multiplicaría por cinco la superficie donde una regla —la unicidad del
/// nombre, o que INACTIVO no borre— podría implementarse de forma distinta por descuido.
///
/// El servicio no filtra por alcance de compañías: los catálogos son globales al sistema, no
/// pertenecen a ninguna compañía (RF-030).
/// </remarks>
public sealed class MasterDataService<TEntity>(IAppDbContext db, IMaestroMetadatos metadatos)
    where TEntity : EntidadMaestra, new()
{
    private readonly DbSet<TEntity> _conjunto = db.Maestro<TEntity>();

    public async Task<IReadOnlyList<MasterItem>> ListarAsync(
        Estado? estado,
        CancellationToken ct = default)
    {
        var consulta = _conjunto.AsNoTracking();

        if (estado is not null)
        {
            consulta = consulta.Where(e => e.Estado == estado);
        }

        return await consulta
            .OrderBy(e => e.Nombre)
            .Select(e => new MasterItem(e.Id, e.Nombre, e.Estado))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<MasterItem> CrearAsync(MasterItemRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nombre = Normalizar(request.Nombre);

        await ValidarNombreAsync(nombre, excluyendo: null, ct).ConfigureAwait(false);

        var entidad = new TEntity { Nombre = nombre, Estado = request.Estado };

        _conjunto.Add(entidad);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new MasterItem(entidad.Id, entidad.Nombre, entidad.Estado);
    }

    public async Task<MasterItem> ActualizarAsync(
        Guid id,
        MasterItemRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entidad = await _conjunto.FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Valor de catálogo no encontrado.");

        var nombre = Normalizar(request.Nombre);

        await ValidarNombreAsync(nombre, excluyendo: id, ct).ConfigureAwait(false);

        entidad.Nombre = nombre;

        // Pasar a INACTIVO no borra ni bloquea el histórico: RF-032 aplica hacia adelante, sobre las
        // asignaciones nuevas, que validan el estado del catálogo en su propio caso de uso.
        entidad.Estado = request.Estado;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new MasterItem(entidad.Id, entidad.Nombre, entidad.Estado);
    }

    private static string Normalizar(string nombre) => nombre.Trim();

    private async Task ValidarNombreAsync(string nombre, Guid? excluyendo, CancellationToken ct)
    {
        var limite = metadatos.LongitudMaximaNombre<TEntity>();

        // El límite se comprueba contra la longitud real de la columna de ESTE catálogo y no contra
        // el máximo genérico del contrato (200): así un nombre demasiado largo se rechaza con un 400
        // explicativo en lugar de reventar como error de truncamiento en la base de datos.
        if (nombre.Length == 0 || nombre.Length > limite)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValidacionEntrada,
                $"El nombre es obligatorio y no puede exceder {limite} caracteres.");
        }

        if (!metadatos.NombreEsUnico<TEntity>())
        {
            return;
        }

        var duplicado = await _conjunto
            .AnyAsync(e => e.Nombre == nombre && (excluyendo == null || e.Id != excluyendo), ct)
            .ConfigureAwait(false);

        if (duplicado)
        {
            throw new ConflictoEstadoException(
                "NOMBRE_YA_REGISTRADO",
                $"Ya existe un valor con el nombre '{nombre}' en este catálogo.");
        }
    }
}

/// <summary>
/// Reglas de cada catálogo que viven en la configuración de persistencia (longitud de columna,
/// unicidad) y que la capa de Aplicación necesita para validar antes de escribir.
/// </summary>
public interface IMaestroMetadatos
{
    int LongitudMaximaNombre<TEntity>() where TEntity : EntidadMaestra;

    bool NombreEsUnico<TEntity>() where TEntity : EntidadMaestra;
}
