using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Companies;

/// <summary>
/// Relaciones temporales Contratista↔Principal (RF-051; contracts/companies.yaml).
/// </summary>
/// <remarks>
/// Una Contratista puede tener relaciones vigentes simultáneas con varias Principales (CS-012); lo
/// que se excluye es más de una vigente para el mismo par. La aplicación cierra la anterior antes de
/// abrir la nueva y devuelve errores legibles; el trigger de no-solapamiento de SQL Server
/// (research.md §5) es la última línea de defensa ante una condición de carrera, no el mecanismo
/// primario.
/// </remarks>
public sealed class RelacionContratistaPrincipalService(
    IAppDbContext db,
    CompaniaService companias,
    IAlcanceCompaniaAccessor alcance,
    IRelojSistema reloj)
{
    public async Task<IReadOnlyList<RelacionContratistaPrincipalDto>> ListarAsync(
        Guid contratistaId,
        CancellationToken ct = default)
    {
        // 404 si no existe, está fuera de alcance, o no es CONTRATISTA (contracts/companies.yaml).
        await ObtenerContratistaAsync(contratistaId, ct).ConfigureAwait(false);

        var enAlcance = alcance.CompaniaIds.ToList();

        // Se listan sólo las relaciones cuya Principal también está dentro del alcance: la Principal
        // es un dato de otra compañía y no debe filtrarse por el hecho de administrar la Contratista.
        return await db.RelacionesContratistaPrincipal
            .AsNoTracking()
            .Where(r => r.CompaniaContratistaId == contratistaId
                        && enAlcance.Contains(r.CompaniaPrincipalId))
            .OrderByDescending(r => r.FechaHoraInicio)
            .Select(r => new RelacionContratistaPrincipalDto(
                r.Id, r.CompaniaContratistaId, r.CompaniaPrincipalId, r.FechaHoraInicio, r.FechaHoraFin))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<RelacionContratistaPrincipalDto> CrearAsync(
        Guid contratistaId,
        RelacionContratistaPrincipalRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ObtenerContratistaAsync(contratistaId, ct).ConfigureAwait(false);

        // La Principal debe existir, estar en alcance y ser PRINCIPAL_MANDANTE (RF-051).
        await companias
            .ObtenerDeTipoAsync(request.CompaniaPrincipalId, TipoCompania.PRINCIPAL_MANDANTE, ct)
            .ConfigureAwait(false);

        var relacion = new RelacionContratistaPrincipal
        {
            CompaniaContratistaId = contratistaId,
            CompaniaPrincipalId = request.CompaniaPrincipalId,
            FechaHoraInicio = request.FechaHoraInicio,
            FechaHoraFin = null,
        };

        await db.EjecutarEnTransaccionAsync(
            async cancelacion =>
            {
                // 1) Cerrar la relación abierta previa del mismo par, si la hay.
                var previa = await db.RelacionesContratistaPrincipal
                    .Where(r => r.CompaniaContratistaId == contratistaId
                                && r.CompaniaPrincipalId == request.CompaniaPrincipalId
                                && r.FechaHoraFin == null)
                    .OrderByDescending(r => r.FechaHoraInicio)
                    .FirstOrDefaultAsync(cancelacion)
                    .ConfigureAwait(false);

                if (previa is not null)
                {
                    if (request.FechaHoraInicio <= previa.FechaHoraInicio)
                    {
                        // Cerrar la previa en un instante anterior a su propio inicio produciría un
                        // intervalo invertido; se rechaza con un mensaje que explica el conflicto en
                        // lugar de dejar que lo aborte el trigger.
                        throw new ConflictoEstadoException(
                            CodigosError.SolapamientoVigencia,
                            "La nueva relación debe iniciar después del inicio de la relación vigente con esa Compañía Principal.");
                    }

                    previa.FechaHoraFin = request.FechaHoraInicio;

                    // El cierre se confirma en su propia sentencia: el trigger de no-solapamiento se
                    // evalúa por sentencia y vería ambas relaciones abiertas si el INSERT se
                    // adelantara al UPDATE (EF no garantiza el orden relativo dentro de un lote).
                    await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
                }

                // 2) Abrir la nueva.
                db.RelacionesContratistaPrincipal.Add(relacion);
                await db.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            },
            ct)
            .ConfigureAwait(false);

        return AMapa(relacion);
    }

    public async Task FinalizarAsync(
        Guid contratistaId,
        Guid relacionId,
        CancellationToken ct = default)
    {
        await ObtenerContratistaAsync(contratistaId, ct).ConfigureAwait(false);

        var relacion = await db.RelacionesContratistaPrincipal
            .FirstOrDefaultAsync(
                r => r.Id == relacionId && r.CompaniaContratistaId == contratistaId, ct)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Relación no encontrada.");

        if (!alcance.EstaEnAlcance(relacion.CompaniaPrincipalId))
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Relación no encontrada.");
        }

        var ahora = reloj.UtcNow;

        // Vigencia evaluada dinámicamente: una relación con FechaHoraFin ya pasada no está vigente
        // aunque nadie la haya "finalizado" (Principio IV).
        if (!relacion.EstaVigenteEn(ahora))
        {
            throw new ConflictoEstadoException(
                "RELACION_NO_VIGENTE",
                "La relación ya no está vigente.");
        }

        relacion.FechaHoraFin = ahora;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<Compania> ObtenerContratistaAsync(Guid contratistaId, CancellationToken ct)
    {
        var compania = await companias
            .ObtenerEnAlcanceAsync(contratistaId, seguimiento: false, ct)
            .ConfigureAwait(false);

        // contracts/companies.yaml documenta 404 —no 400— cuando el id de la ruta no corresponde a
        // una CONTRATISTA: la ruta entera deja de tener sentido, no sólo el cuerpo de la petición.
        if (compania.TipoCompania != TipoCompania.CONTRATISTA)
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "La compañía indicada no es una CONTRATISTA.");
        }

        return compania;
    }

    private static RelacionContratistaPrincipalDto AMapa(RelacionContratistaPrincipal r) =>
        new(r.Id, r.CompaniaContratistaId, r.CompaniaPrincipalId, r.FechaHoraInicio, r.FechaHoraFin);
}
