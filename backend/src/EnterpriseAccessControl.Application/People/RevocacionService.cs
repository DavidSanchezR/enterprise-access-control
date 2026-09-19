using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>Resumen de lo revocado por una cascada, para auditoría y pruebas.</summary>
public sealed record ResultadoRevocacion(
    int ContextosRevocados,
    int UnidadesRevocadas,
    int CredencialesRevocadas);

/// <summary>
/// Revocación automática en cascada al cerrarse una pertenencia Persona–Compañía
/// (RF-061 a RF-065, research.md §14).
/// </summary>
/// <remarks>
/// **Por qué escritura y no solo lectura dinámica**: negocio pidió expresamente que la revocación
/// quede registrada, no que se derive en cada consulta. Escribirla permite auditar "qué se revocó y
/// por qué" de forma directa, y deja constancia incluso en vistas que no pasan por el motor de
/// evaluación de acceso. La re-evaluación dinámica por fechas se conserva como defensa adicional,
/// pero ya no es el único mecanismo (research.md §14.1, que revierte la decisión anterior).
///
/// **Por qué en la misma transacción y no en un proceso aparte**: un trabajo asíncrono abriría una
/// ventana en la que la pertenencia ya terminó pero sus dependientes siguen marcados como vigentes.
/// El volumen por persona es pequeño —a lo sumo unos pocos contextos simultáneos—, así que no hay
/// razón para pagar esa inconsistencia.
///
/// **Por qué `MIN` y nunca una extensión**: si un contexto ya terminaba antes que el cese, la cascada
/// no debe alargarlo hasta la fecha del cese. La regla es acortar, nunca extender.
///
/// **Alcance**: la cascada alcanza los dependientes de *esta* pertenencia, no "todo lo de la
/// persona". Como RF-014 impide dos pertenencias activas a la vez, en la práctica eso equivale a
/// todos los dependientes vigentes en ese instante; la diferencia importa para la auditoría, que
/// queda registrada en <c>RevocadoPorPertenenciaId</c> (research.md §14.4).
///
/// **Fuera de alcance (Decisión Pendiente #6)**: esta cascada NO se dispara al terminar una
/// <c>RelaciónContratistaPrincipal</c> ni al inactivar una <c>Compañía</c>. Ambos casos quedan
/// cubiertos solo por la re-evaluación dinámica hasta que negocio decida (research.md §14.5).
/// </remarks>
public sealed class RevocacionService(IAppDbContext db)
{
    /// <summary>
    /// Revoca los dependientes vigentes de la pertenencia indicada, propagando su fecha de cese.
    /// </summary>
    /// <remarks>
    /// No llama a <c>SaveChanges</c>: forma parte de la misma unidad de trabajo que el cierre de la
    /// pertenencia, y confirmarla por separado rompería la atomicidad que la regla exige.
    /// </remarks>
    public async Task<ResultadoRevocacion> RevocarDependientesAsync(
        AsignacionPersonaCompania pertenencia,
        MotivoFinRevocacion motivo,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pertenencia);

        var efectiva = pertenencia.FechaHoraFin;

        // Se revocan los que aún terminarían después del cese. Los que ya terminaban antes no se
        // tocan: su historia ya está cerrada y reescribirla falsearía la auditoría.
        var contextos = await db.ContextosOperativos
            .Where(c => c.PersonaId == pertenencia.PersonaId
                        && c.Estado == Estado.ACTIVO
                        && c.FechaHoraFin > efectiva)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var contexto in contextos)
        {
            ReglasRevocacion.Cerrar(contexto, efectiva, motivo, pertenencia.Id);
        }

        var contextosRevocadosIds = contextos.ConvertAll(c => c.Id);

        // Las unidades organizativas se alcanzan por su contexto, no por PersonaId: es la dependencia
        // real y la que permite que una asignación de otro contexto quede intacta.
        var unidades = await db.AsignacionesUnidadOrganizativa
            .Where(u => contextosRevocadosIds.Contains(u.ContextoOperativoId)
                        && u.Estado == Estado.ACTIVO
                        && u.FechaHoraFin > efectiva)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var unidad in unidades)
        {
            ReglasRevocacion.Cerrar(unidad, efectiva, motivo, pertenencia.Id);
        }

        // Las credenciales se alcanzan por la Compañía Principal de los contextos revocados: una
        // credencial emitida por una Principal cuyo contexto no se revocó debe sobrevivir.
        var principalesRevocadas = contextos.ConvertAll(c => c.CompaniaPrincipalId);

        var credenciales = await db.AsignacionesCredencial
            .Where(c => c.PersonaId == pertenencia.PersonaId
                        && c.Estado == EstadoCredencial.ASIGNADO
                        && principalesRevocadas.Contains(c.CompaniaPrincipalId)
                        && c.FechaHoraFin > efectiva)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var credencial in credenciales)
        {
            ReglasRevocacion.Revocar(credencial, efectiva, pertenencia.Id);
        }

        return new ResultadoRevocacion(contextos.Count, unidades.Count, credenciales.Count);
    }


}
