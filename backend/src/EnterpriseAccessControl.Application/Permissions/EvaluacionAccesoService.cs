using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnterpriseAccessControl.Application.Permissions;

/// <summary>
/// Orquesta una evaluación de acceso: carga el estado y se lo entrega al motor de dominio
/// (Historia 8, research.md §7, §18).
/// </summary>
/// <remarks>
/// La división es deliberada: aquí vive todo lo que necesita base de datos, y en
/// <see cref="EvaluadorDeAcceso"/> todo lo que es regla de negocio. Así el motor puede probarse paso
/// a paso sin motor relacional (Principio VII) y esta clase se limita a resolver consultas.
///
/// Las consultas siguen el orden del algoritmo y se cortan en cuanto el resultado ya está decidido:
/// no tiene sentido calcular la unidad organizativa vigente de alguien que va a ser denegado por
/// falta de credencial. Eso también sostiene el objetivo de p95 &lt; 500 ms (CS-003).
/// </remarks>
public sealed class EvaluacionAccesoService(
    IAppDbContext db,
    IAlcanceCompaniaAccessor alcance,
    EvaluadorDeAcceso evaluador,
    IOptions<ZonaHorariaOptions> zonaHoraria)
{
    public async Task<EvaluarAccesoResponse> EvaluarAsync(
        EvaluarAccesoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var instante = InstanteUtc.Desde(request.FechaHora);

        var area = await db.AreasAcceso
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AreaAccesoId, ct)
            .ConfigureAwait(false);

        var personaExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PersonaId, ct)
            .ConfigureAwait(false);

        // Persona o área inexistentes, y la falta de alcance sobre la Principal del área, son 404 en
        // el contrato (contracts/access-evaluation.yaml): no llegan a devolverse como un resultado
        // DENEGADO con motivo. El motor sigue sabiendo expresarlos —los necesita para ser evaluable
        // paso a paso— y esta capa los traduce al código HTTP que el contrato declara.
        if (!personaExiste || area is null || !alcance.EstaEnAlcance(area.CompaniaPrincipalId))
        {
            throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Persona o área no encontrada, o fuera del alcance del usuario autenticado.");
        }

        var datos = await ReunirDatosAsync(request.PersonaId, area, instante, ct).ConfigureAwait(false);
        var resultado = evaluador.Evaluar(datos);

        return new EvaluarAccesoResponse(
            resultado.Resultado,
            resultado.MotivoDenegacion,
            resultado.CompaniaPrincipalId,
            resultado.ContextoOperativoId,
            resultado.PermisoAplicadoId,
            resultado.NivelAplicado,
            zonaHoraria.Value.TimeZoneId);
    }

    private async Task<DatosDeEvaluacion> ReunirDatosAsync(
        Guid personaId,
        AreaAcceso area,
        DateTime instante,
        CancellationToken ct)
    {
        // Paso 5: contexto operativo vigente con la Principal propietaria del área.
        var contexto = await db.ContextosOperativos
            .AsNoTracking()
            .Where(c => c.PersonaId == personaId
                        && c.CompaniaPrincipalId == area.CompaniaPrincipalId
                        && c.FechaHoraInicio <= instante
                        && instante < c.FechaHoraFin)
            .OrderByDescending(c => c.FechaHoraInicio)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // Paso 5: compañía de pertenencia vigente en la fecha evaluada, re-derivada (RF-065).
        var pertenencia = await db.AsignacionesPersonaCompania
            .AsNoTracking()
            .Where(a => a.PersonaId == personaId
                        && a.FechaHoraInicio <= instante
                        && instante < a.FechaHoraFin)
            .OrderByDescending(a => a.FechaHoraInicio)
            .Select(a => a.CompaniaId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var compania = pertenencia == Guid.Empty
            ? null
            : await db.Companias
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == pertenencia, ct)
                .ConfigureAwait(false);

        var relacionVigente =
            compania is not null
            && compania.TipoCompania == TipoCompania.CONTRATISTA
            && await db.RelacionesContratistaPrincipal
                .AsNoTracking()
                .AnyAsync(
                    r => r.CompaniaContratistaId == compania.Id
                         && r.CompaniaPrincipalId == area.CompaniaPrincipalId
                         && r.FechaHoraInicio <= instante
                         && (r.FechaHoraFin == null || instante < r.FechaHoraFin),
                    ct)
                .ConfigureAwait(false);

        // Paso 6: credencial vigente de la persona para esa Principal en el instante evaluado
        // (RF-066, RF-070, RF-071): Estado ASIGNADO y FechaHoraInicio <= instante <= FechaHoraFin.
        //
        // Se filtra por el instante y no se toma "la más reciente": una persona puede tener ya
        // registrada la credencial del próximo periodo —sin solaparse con la actual, que es lo que el
        // trigger exige— y elegirla por inicio haría denegar a quien hoy sí tiene credencial vigente.
        // El motor vuelve a comprobar la vigencia; si no hay ninguna, recibe null y deniega.
        var credencial = await db.AsignacionesCredencial
            .AsNoTracking()
            .Where(c => c.PersonaId == personaId
                        && c.CompaniaPrincipalId == area.CompaniaPrincipalId
                        && c.Estado == EstadoCredencial.ASIGNADO
                        && c.FechaHoraInicio <= instante
                        && instante <= c.FechaHoraFin)
            .OrderByDescending(c => c.FechaHoraInicio)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // Paso 8: perfiles vigentes de la persona y perfiles que el área admite.
        var perfiles = await db.AsignacionesTipoPersona
            .AsNoTracking()
            .Where(t => t.PersonaId == personaId
                        && t.FechaHoraInicio <= instante
                        && instante < t.FechaHoraFin)
            .Select(t => t.TipoPersonaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var autorizados = await db.AreasAccesoTipoPersona
            .AsNoTracking()
            .Where(t => t.AreaAccesoId == area.Id)
            .Select(t => t.TipoPersonaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Paso 9: unidad organizativa vigente dentro de ese contexto operativo.
        var unidadId = contexto is null
            ? null
            : await db.AsignacionesUnidadOrganizativa
                .AsNoTracking()
                .Where(u => u.ContextoOperativoId == contexto.Id
                            && u.FechaHoraInicio <= instante
                            && instante < u.FechaHoraFin)
                .OrderByDescending(u => u.FechaHoraInicio)
                .Select(u => (Guid?)u.UnidadOrganizativaId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

        // Paso 10: permisos del área, en los tres niveles. Se acotan ya a los sujetos posibles para
        // no traer los permisos de todas las personas del área.
        var permisos = await db.PermisosAcceso
            .AsNoTracking()
            .Where(p => p.AreaAccesoId == area.Id
                        && (p.PersonaId == personaId
                            || (unidadId != null && p.UnidadOrganizativaId == unidadId)
                            || (compania != null && p.CompaniaId == compania.Id)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var bloques = await db.BloquesHorarioPermiso
            .AsNoTracking()
            .Where(b => permisos.Select(p => p.Id).Contains(b.PermisoAccesoId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var porPermiso = bloques.ToLookup(b => b.PermisoAccesoId);

        return new DatosDeEvaluacion
        {
            FechaHoraUtc = instante,
            PersonaId = personaId,
            Area = area,
            UsuarioTieneAlcanceSobrePrincipal = alcance.EstaEnAlcance(area.CompaniaPrincipalId),
            ContextoOperativo = contexto,
            CompaniaPertenencia = compania,
            RelacionContratistaPrincipalVigente = relacionVigente,
            Credencial = credencial,
            TiposPersonaVigentes = perfiles,
            TiposPersonaAutorizadosEnArea = autorizados,
            UnidadOrganizativaVigenteId = unidadId,
            PermisosDelArea = [.. permisos.Select(p => new PermisoConBloques(p, [.. porPermiso[p.Id]]))],
        };
    }

}
