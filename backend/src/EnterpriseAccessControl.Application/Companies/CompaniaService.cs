using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Companies;

/// <summary>
/// Mantenimiento de compañías y su clasificación PRINCIPAL_MANDANTE/CONTRATISTA
/// (RF-006, RF-042; contracts/companies.yaml).
/// </summary>
/// <remarks>
/// Toda lectura se filtra por el alcance del usuario autenticado (Principio I, RF-049). Un recurso
/// fuera de alcance devuelve 404 y no 403: distinguirlos permitiría confirmar la existencia de
/// compañías ajenas conociendo su identificador.
/// </remarks>
public sealed class CompaniaService(
    IAppDbContext db,
    IAlcanceCompaniaAccessor alcance,
    IRelojEmpresarial reloj,
    DependenciasTipoCompaniaValidator dependencias)
{
    public async Task<PaginaResponse<CompaniaDto>> ListarAsync(
        FiltroCompanias filtro,
        ParametrosPaginacion paginacion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var consulta = db.Companias.AsNoTracking();

        // Un GLOBAL_ADMINISTRATOR no enumera compañías: filtrar por una lista vacía lo dejaría sin
        // ver ninguna (RF-074, RF-077).
        if (!alcance.EsGlobal)
        {
            var enAlcance = alcance.CompaniaIds.ToList();
            consulta = consulta.Where(c => enAlcance.Contains(c.Id));
        }

        if (filtro.Estado is not null)
        {
            consulta = consulta.Where(c => c.Estado == filtro.Estado);
        }

        if (filtro.TipoCompania is not null)
        {
            consulta = consulta.Where(c => c.TipoCompania == filtro.TipoCompania);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Nombre, $"%{texto}%") ||
                EF.Functions.Like(c.NumeroDocumento, $"%{texto}%"));
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var items = await consulta
            .OrderBy(c => c.Nombre)
            .Skip(paginacion.Saltar)
            .Take(paginacion.TamañoPagina)
            .Select(c => new CompaniaDto(
                c.Id,
                c.Nombre,
                c.TipoDocumentoId,
                c.NumeroDocumento,
                c.TipoCompania,
                c.Estado,
                c.ZonaHorariaIana))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PaginaResponse<CompaniaDto>(items, total, paginacion.Pagina, paginacion.TamañoPagina);
    }

    public async Task<CompaniaDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var compania = await ObtenerEnAlcanceAsync(id, seguimiento: false, ct).ConfigureAwait(false);

        return AMapa(compania);
    }

    public async Task<CompaniaDto> CrearAsync(CompaniaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ValidarDocumentoDisponibleAsync(request, excluyendo: null, ct).ConfigureAwait(false);

        ValidarZonaHoraria(request);

        var compania = new Compania
        {
            Nombre = request.Nombre.Trim(),
            TipoDocumentoId = request.TipoDocumentoId,
            NumeroDocumento = request.NumeroDocumento.Trim(),
            TipoCompania = request.TipoCompania,
            Estado = request.Estado,
            ZonaHorariaIana = NormalizarZona(request),
        };

        db.Companias.Add(compania);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(compania);
    }

    public async Task<CompaniaDto> ActualizarAsync(
        Guid id,
        CompaniaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compania = await ObtenerEnAlcanceAsync(id, seguimiento: true, ct).ConfigureAwait(false);

        await ValidarDocumentoDisponibleAsync(request, excluyendo: id, ct).ConfigureAwait(false);

        ValidarZonaHoraria(request);

        // El cambio de clasificación se verifica ANTES de tocar la entidad: si hay dependencias
        // incompatibles se rechaza entero, sin dejar a medias un nombre ya actualizado (RF-081).
        await dependencias
            .ValidarCambioAsync(id, compania.TipoCompania, request.TipoCompania, ct)
            .ConfigureAwait(false);

        compania.Nombre = request.Nombre.Trim();
        compania.TipoDocumentoId = request.TipoDocumentoId;
        compania.NumeroDocumento = request.NumeroDocumento.Trim();
        compania.TipoCompania = request.TipoCompania;
        compania.Estado = request.Estado;
        compania.ZonaHorariaIana = NormalizarZona(request);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(compania);
    }

    /// <summary>
    /// Valida la zona horaria por request y no solo al arrancar el proceso (RF-080).
    /// </summary>
    /// <remarks>
    /// La validación de arranque solo cubría la zona global de configuración. Con una zona por
    /// Compañía Principal, un identificador inválido entraría por la API y no se detectaría hasta la
    /// primera evaluación de bloques horarios de esa Principal, que entonces caería silenciosamente
    /// en la zona de respaldo y daría resultados correctos para la zona equivocada.
    /// </remarks>
    private void ValidarZonaHoraria(CompaniaRequest request)
    {
        var zona = request.ZonaHorariaIana?.Trim();

        if (request.TipoCompania != TipoCompania.PRINCIPAL_MANDANTE)
        {
            // Una CONTRATISTA no la necesita; si de todos modos llega un valor, debe ser válido.
            if (!string.IsNullOrWhiteSpace(zona) && !reloj.EsZonaValida(zona))
            {
                throw new ReglaNegocioInvalidaException(
                    CodigosError.ZonaHorariaInvalida,
                    $"'{zona}' no es un identificador de zona horaria IANA reconocido.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(zona))
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ZonaHorariaRequerida,
                "Una Compañía Principal debe declarar su zona horaria IANA (p. ej. America/Lima).");
        }

        if (!reloj.EsZonaValida(zona))
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ZonaHorariaInvalida,
                $"'{zona}' no es un identificador de zona horaria IANA reconocido.");
        }
    }

    private static string? NormalizarZona(CompaniaRequest request)
    {
        var zona = request.ZonaHorariaIana?.Trim();

        return string.IsNullOrWhiteSpace(zona) ? null : zona;
    }

    /// <summary>
    /// Recupera una compañía exigiendo que esté dentro del alcance del usuario, o lanza 404.
    /// </summary>
    /// <remarks>
    /// Punto único de resolución para el resto de servicios de la historia: así la comprobación de
    /// alcance no se reimplementa —ni se olvida— en cada caso de uso que necesita una compañía.
    /// </remarks>
    public async Task<Compania> ObtenerEnAlcanceAsync(
        Guid id,
        bool seguimiento,
        CancellationToken ct = default)
    {
        if (!alcance.EstaEnAlcance(id))
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Compañía no encontrada.");
        }

        var consulta = seguimiento ? db.Companias : db.Companias.AsNoTracking();

        return await consulta.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Compañía no encontrada.");
    }

    /// <summary>
    /// Recupera una compañía en alcance exigiendo además una clasificación concreta (RF-045, RF-051).
    /// </summary>
    /// <remarks>
    /// El tipo equivocado es un 400 y no un 404: la compañía existe y el usuario puede verla; lo que
    /// no encaja es la operación solicitada, y ocultarlo no protege nada.
    /// </remarks>
    public async Task<Compania> ObtenerDeTipoAsync(
        Guid id,
        TipoCompania tipoEsperado,
        CancellationToken ct = default)
    {
        var compania = await ObtenerEnAlcanceAsync(id, seguimiento: false, ct).ConfigureAwait(false);

        if (compania.TipoCompania != tipoEsperado)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.CompaniaDebeSerPrincipal,
                $"La compañía '{compania.Nombre}' es {compania.TipoCompania} y la operación requiere {tipoEsperado}.");
        }

        return compania;
    }

    private async Task ValidarDocumentoDisponibleAsync(
        CompaniaRequest request,
        Guid? excluyendo,
        CancellationToken ct)
    {
        var numero = request.NumeroDocumento.Trim();

        // La unicidad la respalda un índice único; comprobarla antes permite devolver un 409 con un
        // código de negocio en vez de dejar escapar una violación de índice.
        var duplicada = await db.Companias
            .AnyAsync(
                c => c.TipoDocumentoId == request.TipoDocumentoId
                     && c.NumeroDocumento == numero
                     && (excluyendo == null || c.Id != excluyendo),
                ct)
            .ConfigureAwait(false);

        if (duplicada)
        {
            throw new ConflictoEstadoException(
                "DOCUMENTO_YA_REGISTRADO",
                "Ya existe una compañía con ese tipo y número de documento.");
        }
    }

    private static CompaniaDto AMapa(Compania c) =>
        new(c.Id, c.Nombre, c.TipoDocumentoId, c.NumeroDocumento, c.TipoCompania, c.Estado, c.ZonaHorariaIana);
}
