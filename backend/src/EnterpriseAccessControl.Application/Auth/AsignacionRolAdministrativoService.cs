using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>
/// Ciclo de vida de las asignaciones de rol administrativo (RF-074, RF-075).
/// </summary>
/// <remarks>
/// Dueño de los **invariantes de dominio** de la asignación: regla fundamental de
/// <c>CompañíaId</c>, vigencia obligatoria y real, no-solapamiento y reglas de renovación. No decide
/// *quién* puede asignar qué —eso es RF-076 y lo aplica <see cref="UsuarioService"/>, que conoce al
/// solicitante—. Esa separación es lo que permite que la rutina de arranque (RF-078) reutilice este
/// servicio: en el arranque no hay solicitante autenticado al que aplicarle RF-076, pero los
/// invariantes de la asignación sembrada deben cumplirse igual.
/// </remarks>
public sealed class AsignacionRolAdministrativoService(IAppDbContext db, IRelojSistema reloj)
{
    public async Task<IReadOnlyList<AsignacionRolAdministrativoDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var ahora = reloj.UtcNow;

        var asignaciones = await db.AsignacionesRolAdministrativo
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .OrderByDescending(a => a.FechaHoraInicio)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return asignaciones.Select(a => AMapa(a, ahora)).ToList();
    }

    /// <summary>Asignaciones vigentes en este instante, para resolver el alcance efectivo (RF-077).</summary>
    public async Task<IReadOnlyList<AsignacionRolAdministrativo>> ObtenerVigentesAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var ahora = reloj.UtcNow;

        return await db.AsignacionesRolAdministrativo
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId
                        && a.FechaHoraInicio <= ahora
                        && ahora < a.FechaHoraFin)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<AsignacionRolAdministrativoDto> CrearAsync(
        Guid usuarioId,
        AsignarRolRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asignacion = await ConstruirValidadaAsync(usuarioId, request, ct).ConfigureAwait(false);

        db.AsignacionesRolAdministrativo.Add(asignacion);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(asignacion, reloj.UtcNow);
    }

    /// <summary>
    /// Valida y construye la asignación sin persistirla, para componerla con otras escrituras.
    /// </summary>
    /// <remarks>
    /// La usa el alta de usuario, que debe insertar el <c>Usuario</c> y su primera asignación en el
    /// mismo <c>SaveChanges</c>: crear el usuario y fallar después al asignarle su rol lo dejaría sin
    /// ninguna autorización y sin forma de repararlo desde la propia interfaz.
    /// </remarks>
    public async Task<AsignacionRolAdministrativo> ConstruirValidadaAsync(
        Guid usuarioId,
        AsignarRolRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asignacion = new AsignacionRolAdministrativo
        {
            UsuarioId = usuarioId,
            Rol = request.Rol,
            CompaniaId = request.CompaniaId,
            FechaHoraInicio = request.FechaHoraInicio,
            FechaHoraFin = request.FechaHoraFin,
        };

        ValidarReglaFundamental(asignacion);
        ValidarVigencia(asignacion.FechaHoraInicio, asignacion.FechaHoraFin);

        await ValidarCompaniaAsignableAsync(asignacion, ct).ConfigureAwait(false);
        await ValidarSinSolapamientoAsync(asignacion, ct).ConfigureAwait(false);

        return asignacion;
    }

    /// <summary>
    /// Finaliza anticipadamente una asignación vigente fijando su fin al instante actual (RF-075).
    /// </summary>
    /// <remarks>
    /// Acorta la vigencia; nunca borra la fila. El histórico de quién administró qué y hasta cuándo
    /// se conserva íntegro (Principio IV).
    /// </remarks>
    public async Task FinalizarAsync(
        Guid usuarioId,
        Guid asignacionId,
        CancellationToken ct = default)
    {
        var asignacion = await ObtenerAsync(usuarioId, asignacionId, ct).ConfigureAwait(false);

        var ahora = reloj.UtcNow;

        if (!asignacion.EstaVigenteEn(ahora))
        {
            throw new ConflictoEstadoException(
                CodigosError.AsignacionRolNoVigente,
                "La asignación de rol ya no está vigente; no puede finalizarse.");
        }

        asignacion.FechaHoraFin = ahora;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Extiende la vigencia de una asignación, con las mismas reglas que la renovación de
    /// pertenencia (RF-073, RF-075).
    /// </summary>
    /// <remarks>
    /// Solo hacia adelante y solo mientras siga vigente dinámicamente: renovar una asignación ya
    /// expirada puentearía retroactivamente un intervalo en el que el usuario no tuvo autorización.
    /// </remarks>
    public async Task<AsignacionRolAdministrativoDto> RenovarAsync(
        Guid usuarioId,
        Guid asignacionId,
        RenovarAsignacionRolRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asignacion = await ObtenerAsync(usuarioId, asignacionId, ct).ConfigureAwait(false);

        var ahora = reloj.UtcNow;

        if (!asignacion.EsRenovableEn(ahora))
        {
            throw new ConflictoEstadoException(
                CodigosError.AsignacionRolNoVigente,
                "La asignación ya expiró; una renovación crearía un vacío retroactivo de autorización. "
                + "Cree una asignación nueva.");
        }

        // Conflicto de estado (409), no validación de entrada (400): es el mismo criterio que
        // HistorialPersonaService aplica a la renovación de pertenencia con este mismo código de
        // negocio. Un código de error no puede significar dos estados HTTP según la entidad.
        if (request.FechaHoraFin <= asignacion.FechaHoraFin)
        {
            throw new ConflictoEstadoException(
                CodigosError.RenovacionNoPosterior,
                "La nueva fecha de fin debe ser estrictamente posterior a la vigente.");
        }

        asignacion.FechaHoraFin = request.FechaHoraFin;

        await ValidarSinSolapamientoAsync(asignacion, ct).ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(asignacion, ahora);
    }

    public async Task<AsignacionRolAdministrativo> ObtenerAsync(
        Guid usuarioId,
        Guid asignacionId,
        CancellationToken ct = default) =>
        await db.AsignacionesRolAdministrativo
            .FirstOrDefaultAsync(a => a.Id == asignacionId && a.UsuarioId == usuarioId, ct)
            .ConfigureAwait(false)
        ?? throw new RecursoNoEncontradoException(
            CodigosError.RecursoNoEncontrado,
            "Asignación de rol no encontrada.");

    /// <summary>Regla fundamental de <c>CompañíaId</c> frente al rol (RF-074).</summary>
    private static void ValidarReglaFundamental(AsignacionRolAdministrativo asignacion)
    {
        if (asignacion.CumpleReglaFundamental())
        {
            return;
        }

        var detalle = asignacion.Rol == RolAdministrativo.GLOBAL_ADMINISTRATOR
            ? "Un GLOBAL_ADMINISTRATOR no puede tener compañía: su alcance es global."
            : "Un COMPANY_ADMINISTRATOR exige una compañía concreta.";

        throw new ReglaNegocioInvalidaException(CodigosError.RolCompaniaInconsistente, detalle);
    }

    /// <summary>Vigencia obligatoria y real; nunca null ni fecha centinela (RF-075).</summary>
    private static void ValidarVigencia(DateTime inicio, DateTime fin)
    {
        if (fin <= inicio)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.PeriodoInvalido,
                "La fecha de fin debe ser posterior a la de inicio.");
        }
    }

    /// <summary>
    /// La compañía debe existir y estar ACTIVA para incorporarse a una asignación nueva (RF-032).
    /// </summary>
    private async Task ValidarCompaniaAsignableAsync(
        AsignacionRolAdministrativo asignacion,
        CancellationToken ct)
    {
        if (asignacion.CompaniaId is not Guid companiaId)
        {
            return;
        }

        var activa = await db.Companias
            .AnyAsync(c => c.Id == companiaId && c.Estado == Estado.ACTIVO, ct)
            .ConfigureAwait(false);

        if (!activa)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValorMaestroInactivo,
                "La compañía indicada no existe o está inactiva.");
        }
    }

    /// <summary>
    /// Sin solapamiento entre asignaciones COMPANY_ADMINISTRATOR del mismo par (usuario, compañía).
    /// </summary>
    /// <remarks>
    /// <c>GLOBAL_ADMINISTRATOR</c> queda exento de esta partición a propósito (RF-075): pueden
    /// coexistir varios administradores globales, y un mismo usuario puede tener asignaciones
    /// globales consecutivas sin que solaparlas represente una inconsistencia de alcance.
    /// Consecutivas sí se admiten: la comparación usa intervalos semiabiertos.
    /// </remarks>
    private async Task ValidarSinSolapamientoAsync(
        AsignacionRolAdministrativo asignacion,
        CancellationToken ct)
    {
        if (asignacion.Rol != RolAdministrativo.COMPANY_ADMINISTRATOR)
        {
            return;
        }

        var solapa = await db.AsignacionesRolAdministrativo
            .AnyAsync(
                a => a.Id != asignacion.Id
                     && a.UsuarioId == asignacion.UsuarioId
                     && a.CompaniaId == asignacion.CompaniaId
                     && a.Rol == RolAdministrativo.COMPANY_ADMINISTRATOR
                     && a.FechaHoraInicio < asignacion.FechaHoraFin
                     && asignacion.FechaHoraInicio < a.FechaHoraFin,
                ct)
            .ConfigureAwait(false);

        if (solapa)
        {
            throw new ConflictoEstadoException(
                CodigosError.SolapamientoVigencia,
                "Ya existe una asignación COMPANY_ADMINISTRATOR que se solapa para ese usuario y compañía.");
        }
    }

    internal static AsignacionRolAdministrativoDto AMapa(
        AsignacionRolAdministrativo a,
        DateTime ahora) =>
        new(a.Id, a.UsuarioId, a.Rol, a.CompaniaId, a.FechaHoraInicio, a.FechaHoraFin, a.EstaVigenteEn(ahora));
}
