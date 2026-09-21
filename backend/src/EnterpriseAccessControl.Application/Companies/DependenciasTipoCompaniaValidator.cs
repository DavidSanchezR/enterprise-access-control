using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Companies;

/// <summary>Una dependencia que impide reclasificar una compañía (RF-081).</summary>
public sealed record DependenciaIncompatible(string Categoria, int Cantidad);

/// <summary>
/// Verifica que una compañía no tenga dependencias de dominio incompatibles con el tipo destino
/// antes de permitir el cambio de <c>TipoCompañía</c> (RF-081).
/// </summary>
/// <remarks>
/// **Nunca resuelve nada en cascada**: cuenta, informa y deja que un humano decida. Eliminar áreas,
/// cerrar contextos o revocar credenciales automáticamente por un cambio de clasificación destruiría
/// histórico que el Principio IV exige conservar.
///
/// La categoría de relaciones empresariales cuenta **únicamente** filas reales de
/// <c>RelaciónContratistaPrincipal</c> y se evalúa de forma simétrica: una Contratista con relaciones
/// vigentes *como Contratista* queda bloqueada para pasar a PRINCIPAL_MANDANTE, igual que una
/// Principal con relaciones vigentes *como Principal* lo está para pasar a CONTRATISTA. No se
/// inspecciona <c>AsignaciónPersonaCompañía</c> ni personas: una persona empleada directamente por
/// una Principal se modela con pertenencia más contexto operativo auto-fijado (RF-053), nunca como
/// una relación de una compañía consigo misma.
/// </remarks>
public sealed class DependenciasTipoCompaniaValidator(IAppDbContext db, IRelojSistema reloj)
{
    public async Task<IReadOnlyList<DependenciaIncompatible>> DetectarAsync(
        Guid companiaId,
        TipoCompania tipoDestino,
        CancellationToken ct = default)
    {
        var ahora = reloj.UtcNow;
        var incompatibles = new List<DependenciaIncompatible>();

        if (tipoDestino == TipoCompania.CONTRATISTA)
        {
            // Todo lo que solo una PRINCIPAL_MANDANTE puede poseer bloquea el paso a CONTRATISTA.
            await AgregarSiHayAsync(
                incompatibles,
                "Áreas de acceso",
                db.AreasAcceso.Where(a => a.CompaniaPrincipalId == companiaId),
                ct).ConfigureAwait(false);

            await AgregarSiHayAsync(
                incompatibles,
                "Raíces de unidad organizativa",
                db.RaicesUnidadOrganizativa.Where(r => r.CompaniaId == companiaId),
                ct).ConfigureAwait(false);

            await AgregarSiHayAsync(
                incompatibles,
                "Relaciones vigentes como Principal",
                db.RelacionesContratistaPrincipal.Where(r =>
                    r.CompaniaPrincipalId == companiaId
                    && r.FechaHoraInicio <= ahora
                    && (r.FechaHoraFin == null || ahora < r.FechaHoraFin)),
                ct).ConfigureAwait(false);

            await AgregarSiHayAsync(
                incompatibles,
                "Contextos operativos",
                db.ContextosOperativos.Where(c => c.CompaniaPrincipalId == companiaId),
                ct).ConfigureAwait(false);

            await AgregarSiHayAsync(
                incompatibles,
                "Credenciales",
                db.AsignacionesCredencial.Where(c => c.CompaniaPrincipalId == companiaId),
                ct).ConfigureAwait(false);
        }
        else
        {
            // Simetría (D6): las relaciones vigentes en las que actúa como Contratista bloquean el
            // paso a PRINCIPAL_MANDANTE.
            await AgregarSiHayAsync(
                incompatibles,
                "Relaciones vigentes como Contratista",
                db.RelacionesContratistaPrincipal.Where(r =>
                    r.CompaniaContratistaId == companiaId
                    && r.FechaHoraInicio <= ahora
                    && (r.FechaHoraFin == null || ahora < r.FechaHoraFin)),
                ct).ConfigureAwait(false);
        }

        return incompatibles;
    }

    /// <summary>Rechaza el cambio con 409 y detalle accionable si hay dependencias (RF-081, RF-033).</summary>
    public async Task ValidarCambioAsync(
        Guid companiaId,
        TipoCompania tipoActual,
        TipoCompania tipoDestino,
        CancellationToken ct = default)
    {
        if (tipoActual == tipoDestino)
        {
            return;
        }

        var incompatibles = await DetectarAsync(companiaId, tipoDestino, ct).ConfigureAwait(false);

        if (incompatibles.Count == 0)
        {
            return;
        }

        var detalle = string.Join("; ", incompatibles.Select(d => $"{d.Categoria}: {d.Cantidad}"));

        throw new ConflictoEstadoException(
            CodigosError.CambioTipoCompaniaConDependencias,
            $"No se puede cambiar el tipo a {tipoDestino} mientras existan dependencias incompatibles. "
            + $"Resuélvalas primero — {detalle}.");
    }

    private static async Task AgregarSiHayAsync<T>(
        List<DependenciaIncompatible> destino,
        string categoria,
        IQueryable<T> consulta,
        CancellationToken ct)
    {
        var cantidad = await consulta.CountAsync(ct).ConfigureAwait(false);

        if (cantidad > 0)
        {
            destino.Add(new DependenciaIncompatible(categoria, cantidad));
        }
    }
}
