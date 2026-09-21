using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>
/// El solicitante no puede asignar ese rol o esa compañía (403, RF-076).
/// </summary>
/// <remarks>
/// Es 403 y no 404 a propósito: a diferencia de una lectura fuera de alcance, aquí el solicitante ya
/// conoce el recurso —lo está intentando modificar— y ocultarle el motivo solo le haría reintentar.
/// El contrato lo declara así en <c>contracts/users.yaml</c>.
/// </remarks>
public sealed class RolNoAutorizadoException(string mensaje)
    : ErrorNegocioException(CodigosError.RolNoAutorizado, mensaje, 403);

/// <summary>
/// Mantenimiento de usuarios y de sus asignaciones de rol administrativo
/// (RF-004, RF-030, RF-074 a RF-077; contracts/users.yaml).
/// </summary>
/// <remarks>
/// Dueño de RF-076 y del Resource Ownership de <c>Usuario</c>: decide **quién** puede ver y
/// administrar a quién. Los invariantes de la asignación en sí (regla fundamental, vigencia,
/// solapamiento, renovación) viven en <see cref="AsignacionRolAdministrativoService"/>.
///
/// Hasta la Sesión 2026-09-20 este servicio no aplicaba ningún control de alcance: cualquier usuario
/// autenticado podía listar, leer y modificar cualquier otro. Era el defecto crítico F-01 y su
/// corrección es el contenido de esta clase.
///
/// No contiene ninguna dependencia hacia entidades operacionales de <c>Persona</c>: el alcance
/// administrativo es independiente de la relación Persona→Compañía→UnidadOrganizativa (RF-050).
/// </remarks>
public sealed class UsuarioService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IRelojSistema reloj,
    PasswordPolicyValidator politicaValidator,
    IAlcanceCompaniaAccessor alcance,
    AsignacionRolAdministrativoService asignaciones)
{
    /// <summary>
    /// Listado paginado dentro del alcance del solicitante, con filtros opcionales (RF-077, UX-22).
    /// </summary>
    /// <remarks>
    /// El orden de composición es parte del contrato, no un detalle de implementación: alcance →
    /// filtros → total → página. La búsqueda se aplica sobre el conjunto **ya restringido** al alcance
    /// y **antes** de paginar, de modo que un usuario que caería en la página 3 se encuentra igual; y
    /// nunca puede ampliar el alcance, porque es un `Where` adicional sobre una consulta ya acotada, no
    /// un punto de entrada alternativo.
    /// </remarks>
    public async Task<PaginaResponse<UsuarioDto>> ListarAsync(
        FiltroUsuarios filtro,
        ParametrosPaginacion paginacion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var ahora = reloj.UtcNow;

        var consulta = AplicarAlcance(db.Usuarios.AsNoTracking(), ahora);

        if (filtro.Estado is not null)
        {
            consulta = consulta.Where(u => u.Estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // Misma convención que CompaniaService: LIKE con comodines a ambos lados, resuelto por
            // SQL Server con la colación de la base (insensible a mayúsculas por defecto).
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(u => EF.Functions.Like(u.Correo, $"%{texto}%"));
        }

        // El total se cuenta sobre la consulta ya filtrada: informar el total global revelaría
        // cuántos usuarios existen fuera del alcance (UX-22, CS-037). Una búsqueda sin coincidencias
        // devuelve una página vacía, nunca 404: un correo ajeno y un correo inexistente deben ser
        // indistinguibles para que la búsqueda no sirva como oráculo de enumeración.
        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var usuarios = await consulta
            .OrderBy(u => u.Correo)
            .Skip(paginacion.Saltar)
            .Take(paginacion.TamañoPagina)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ids = usuarios.Select(u => u.Id).ToList();

        // Una sola consulta para las asignaciones de toda la página, en lugar de una por usuario.
        var vigentes = await db.AsignacionesRolAdministrativo
            .AsNoTracking()
            .Where(a => ids.Contains(a.UsuarioId)
                        && a.FechaHoraInicio <= ahora
                        && ahora < a.FechaHoraFin)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var items = usuarios
            .Select(u => AMapa(u, vigentes.Where(a => a.UsuarioId == u.Id), ahora))
            .ToList();

        return new PaginaResponse<UsuarioDto>(items, total, paginacion.Pagina, paginacion.TamañoPagina);
    }

    public async Task<UsuarioDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var ahora = reloj.UtcNow;
        var usuario = await ObtenerEnAlcanceAsync(id, seguimiento: false, ahora, ct).ConfigureAwait(false);

        return AMapa(usuario, await VigentesDeAsync(id, ahora, ct).ConfigureAwait(false), ahora);
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Se verifica la autorización antes que nada: si el solicitante no puede asignar ese rol, no
        // tiene sentido validar el correo ni la contraseña (RF-076).
        ValidarPuedeAsignar(request.Rol, request.CompaniaId);

        var correo = request.Correo.Trim();
        var correoNormalizado = Usuario.NormalizarCorreo(correo);

        var yaExiste = await db.Usuarios
            .AnyAsync(u => u.CorreoNormalizado == correoNormalizado, ct)
            .ConfigureAwait(false);

        if (yaExiste)
        {
            throw new ConflictoEstadoException("CORREO_YA_REGISTRADO", "Ya existe un usuario con ese correo.");
        }

        var politica = politicaValidator.Validar(request.PasswordInicial);
        if (!politica.EsValida)
        {
            throw new ReglaNegocioInvalidaException(
                "PASSWORD_NO_CUMPLE_POLITICA",
                string.Join(' ', politica.Errores));
        }

        var ahora = reloj.UtcNow;

        var usuario = new Usuario
        {
            Correo = correo,
            PasswordHash = hasher.Hash(request.PasswordInicial),
            Estado = EstadoUsuario.ACTIVO,
            // La contraseña la fija un administrador: el titular debe cambiarla en su primer acceso.
            RequiereCambioPassword = true,
            FechaUltimoCambioPassword = ahora,
        };

        var asignacion = await asignaciones
            .ConstruirValidadaAsync(
                usuario.Id,
                new AsignarRolRequest(
                    request.Rol,
                    request.CompaniaId,
                    request.FechaHoraInicio,
                    request.FechaHoraFin),
                ct)
            .ConfigureAwait(false);

        // Usuario y primera asignación se escriben juntos: un usuario sin ninguna asignación no
        // podría autenticarse con alcance y nadie podría repararlo desde la propia interfaz.
        db.Usuarios.Add(usuario);
        db.AsignacionesRolAdministrativo.Add(asignacion);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(usuario, [asignacion], ahora);
    }

    public async Task<UsuarioDto> ActualizarAsync(
        Guid id,
        ActualizarUsuarioRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ahora = reloj.UtcNow;
        var usuario = await ObtenerEnAlcanceAsync(id, seguimiento: true, ahora, ct).ConfigureAwait(false);

        var correo = request.Correo.Trim();
        var correoNormalizado = Usuario.NormalizarCorreo(correo);

        var correoEnUso = await db.Usuarios
            .AnyAsync(u => u.Id != id && u.CorreoNormalizado == correoNormalizado, ct)
            .ConfigureAwait(false);

        if (correoEnUso)
        {
            throw new ConflictoEstadoException("CORREO_YA_REGISTRADO", "Ya existe otro usuario con ese correo.");
        }

        usuario.Correo = correo;
        usuario.Estado = request.Estado;

        // Reactivar administrativamente a un usuario bloqueado también limpia su contador: de lo
        // contrario volvería a bloquearse al primer fallo.
        if (request.Estado == EstadoUsuario.ACTIVO)
        {
            usuario.IntentosFallidosConsecutivos = 0;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(usuario, await VigentesDeAsync(id, ahora, ct).ConfigureAwait(false), ahora);
    }

    public async Task DesbloquearAsync(Guid id, CancellationToken ct = default)
    {
        var ahora = reloj.UtcNow;
        var usuario = await ObtenerEnAlcanceAsync(id, seguimiento: true, ahora, ct).ConfigureAwait(false);

        usuario.Estado = EstadoUsuario.ACTIVO;
        usuario.IntentosFallidosConsecutivos = 0;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Asignaciones de rol del usuario, vigentes e históricas (contracts/users.yaml).</summary>
    public async Task<IReadOnlyList<AsignacionRolAdministrativoDto>> ListarRolesAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        await ObtenerEnAlcanceAsync(usuarioId, seguimiento: false, reloj.UtcNow, ct).ConfigureAwait(false);

        return await asignaciones.ListarAsync(usuarioId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Agrega una asignación de rol al usuario; nunca reemplaza las existentes (RF-074, RF-076).
    /// </summary>
    public async Task<AsignacionRolAdministrativoDto> AsignarRolAsync(
        Guid usuarioId,
        AsignarRolRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ObtenerEnAlcanceAsync(usuarioId, seguimiento: false, reloj.UtcNow, ct).ConfigureAwait(false);

        ValidarPuedeAsignar(request.Rol, request.CompaniaId);

        return await asignaciones.CrearAsync(usuarioId, request, ct).ConfigureAwait(false);
    }

    /// <summary>Finaliza anticipadamente una asignación vigente (RF-075, RF-076).</summary>
    public async Task FinalizarRolAsync(
        Guid usuarioId,
        Guid asignacionId,
        CancellationToken ct = default)
    {
        await ObtenerEnAlcanceAsync(usuarioId, seguimiento: false, reloj.UtcNow, ct).ConfigureAwait(false);

        var asignacion = await asignaciones.ObtenerAsync(usuarioId, asignacionId, ct).ConfigureAwait(false);

        // Quien no podría crear esa asignación tampoco puede retirarla: de lo contrario un
        // COMPANY_ADMINISTRATOR podría desactivar a un administrador global (RF-076).
        ValidarPuedeAsignar(asignacion.Rol, asignacion.CompaniaId);

        await asignaciones.FinalizarAsync(usuarioId, asignacionId, ct).ConfigureAwait(false);
    }

    /// <summary>Extiende la vigencia de una asignación (RF-073, RF-075, RF-076).</summary>
    public async Task<AsignacionRolAdministrativoDto> RenovarRolAsync(
        Guid usuarioId,
        Guid asignacionId,
        RenovarAsignacionRolRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ObtenerEnAlcanceAsync(usuarioId, seguimiento: false, reloj.UtcNow, ct).ConfigureAwait(false);

        var asignacion = await asignaciones.ObtenerAsync(usuarioId, asignacionId, ct).ConfigureAwait(false);

        ValidarPuedeAsignar(asignacion.Rol, asignacion.CompaniaId);

        return await asignaciones.RenovarAsync(usuarioId, asignacionId, request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Restricciones de delegación y elevación del COMPANY_ADMINISTRATOR (RF-076).
    /// </summary>
    /// <remarks>
    /// Un COMPANY_ADMINISTRATOR **sí** puede crear pares de su mismo nivel en su propia compañía:
    /// lo que no puede es asignar GLOBAL_ADMINISTRATOR, ni administrar otra compañía, ni por tanto
    /// elevarse a sí mismo. Un usuario sin rol vigente no puede asignar nada.
    /// </remarks>
    private void ValidarPuedeAsignar(RolAdministrativo rol, Guid? companiaId)
    {
        if (alcance.EsGlobal)
        {
            return;
        }

        if (alcance.Rol != RolAdministrativo.COMPANY_ADMINISTRATOR)
        {
            throw new RolNoAutorizadoException(
                "Se requiere una asignación de rol administrativo vigente para asignar roles.");
        }

        if (rol == RolAdministrativo.GLOBAL_ADMINISTRATOR)
        {
            throw new RolNoAutorizadoException(
                "Un COMPANY_ADMINISTRATOR no puede asignar el rol GLOBAL_ADMINISTRATOR.");
        }

        if (companiaId is not Guid destino || !alcance.CompaniaIds.Contains(destino))
        {
            throw new RolNoAutorizadoException(
                "Un COMPANY_ADMINISTRATOR solo puede asignar roles en su propia compañía.");
        }
    }

    /// <summary>
    /// Resource Ownership de <c>Usuario</c>: fuera de alcance responde 404, nunca 403 (RF-077).
    /// </summary>
    private async Task<Usuario> ObtenerEnAlcanceAsync(
        Guid id,
        bool seguimiento,
        DateTime ahora,
        CancellationToken ct)
    {
        var consulta = seguimiento ? db.Usuarios : db.Usuarios.AsNoTracking();

        return await AplicarAlcance(consulta, ahora).FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Usuario no encontrado.");
    }

    /// <summary>
    /// Restringe una consulta de usuarios al alcance efectivo del solicitante (RF-077).
    /// </summary>
    /// <remarks>
    /// Un COMPANY_ADMINISTRATOR ve a quienes tienen alguna asignación **vigente** en alguna de sus
    /// compañías. Las asignaciones ya vencidas no bastan: administrar a alguien que hoy no pertenece
    /// a su compañía sería exactamente el tipo de fuga de alcance que RF-077 cierra.
    /// </remarks>
    private IQueryable<Usuario> AplicarAlcance(IQueryable<Usuario> consulta, DateTime ahora)
    {
        if (alcance.EsGlobal)
        {
            return consulta;
        }

        var propias = alcance.CompaniaIds.ToList();

        return consulta.Where(u => db.AsignacionesRolAdministrativo.Any(a =>
            a.UsuarioId == u.Id
            && a.CompaniaId != null
            && propias.Contains(a.CompaniaId.Value)
            && a.FechaHoraInicio <= ahora
            && ahora < a.FechaHoraFin));
    }

    private async Task<IReadOnlyList<AsignacionRolAdministrativo>> VigentesDeAsync(
        Guid usuarioId,
        DateTime ahora,
        CancellationToken ct) =>
        await db.AsignacionesRolAdministrativo
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId
                        && a.FechaHoraInicio <= ahora
                        && ahora < a.FechaHoraFin)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    private static UsuarioDto AMapa(
        Usuario usuario,
        IEnumerable<AsignacionRolAdministrativo> vigentes,
        DateTime ahora) =>
        new(
            usuario.Id,
            usuario.Correo,
            usuario.Estado,
            usuario.RequiereCambioPassword,
            [.. vigentes.Select(a => AsignacionRolAdministrativoService.AMapa(a, ahora))]);
}
