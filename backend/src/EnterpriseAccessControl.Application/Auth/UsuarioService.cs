using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>
/// Mantenimiento de usuarios y de su alcance de compañías administrables (RF-004, RF-030;
/// contracts/users.yaml).
/// </summary>
/// <remarks>
/// No contiene ninguna dependencia hacia entidades operacionales de <c>Persona</c>: el alcance
/// administrativo es independiente de la relación Persona→Compañía→UnidadOrganizativa (RF-050).
/// </remarks>
public sealed class UsuarioService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IRelojSistema reloj,
    PasswordPolicyValidator politicaValidator)
{
    public async Task<PaginaResponse<UsuarioDto>> ListarAsync(
        EstadoUsuario? estado,
        ParametrosPaginacion paginacion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paginacion);

        var consulta = db.Usuarios.AsNoTracking();

        if (estado is not null)
        {
            consulta = consulta.Where(u => u.Estado == estado);
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var usuarios = await consulta
            .OrderBy(u => u.Correo)
            .Skip(paginacion.Saltar)
            .Take(paginacion.TamañoPagina)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ids = usuarios.Select(u => u.Id).ToList();

        // Una sola consulta para el alcance de toda la página, en lugar de una por usuario (N+1).
        var alcances = await db.AlcancesUsuarioCompania
            .AsNoTracking()
            .Where(a => ids.Contains(a.UsuarioId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var items = usuarios
            .Select(u => AMapa(u, alcances.Where(a => a.UsuarioId == u.Id).Select(a => a.CompaniaId).ToList()))
            .ToList();

        return new PaginaResponse<UsuarioDto>(items, total, paginacion.Pagina, paginacion.TamañoPagina);
    }

    public async Task<UsuarioDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(CodigosError.RecursoNoEncontrado, "Usuario no encontrado.");

        return AMapa(usuario, await ObtenerAlcanceAsync(id, ct).ConfigureAwait(false));
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        await ValidarCompaniasAsync(request.CompaniaIds, ct).ConfigureAwait(false);

        var usuario = new Usuario
        {
            Correo = correo,
            PasswordHash = hasher.Hash(request.PasswordInicial),
            Estado = EstadoUsuario.ACTIVO,
            // La contraseña la fija un administrador: el titular debe cambiarla en su primer acceso.
            RequiereCambioPassword = true,
            FechaUltimoCambioPassword = reloj.UtcNow,
        };

        db.Usuarios.Add(usuario);

        foreach (var companiaId in request.CompaniaIds.Distinct())
        {
            db.AlcancesUsuarioCompania.Add(new AlcanceUsuarioCompania
            {
                UsuarioId = usuario.Id,
                CompaniaId = companiaId,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(usuario, request.CompaniaIds.Distinct().ToList());
    }

    public async Task<UsuarioDto> ActualizarAsync(
        Guid id,
        ActualizarUsuarioRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(CodigosError.RecursoNoEncontrado, "Usuario no encontrado.");

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

        return AMapa(usuario, await ObtenerAlcanceAsync(id, ct).ConfigureAwait(false));
    }

    public async Task DesbloquearAsync(Guid id, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(CodigosError.RecursoNoEncontrado, "Usuario no encontrado.");

        usuario.Estado = EstadoUsuario.ACTIVO;
        usuario.IntentosFallidosConsecutivos = 0;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ObtenerAlcanceAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.AlcancesUsuarioCompania
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .Select(a => a.CompaniaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task ReemplazarAlcanceAsync(
        Guid usuarioId,
        ReemplazarAlcanceRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existe = await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct).ConfigureAwait(false);
        if (!existe)
        {
            throw new RecursoNoEncontradoException(CodigosError.RecursoNoEncontrado, "Usuario no encontrado.");
        }

        await ValidarCompaniasAsync(request.CompaniaIds, ct).ConfigureAwait(false);

        var actuales = await db.AlcancesUsuarioCompania
            .Where(a => a.UsuarioId == usuarioId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        db.AlcancesUsuarioCompania.RemoveRange(actuales);

        foreach (var companiaId in request.CompaniaIds.Distinct())
        {
            db.AlcancesUsuarioCompania.Add(new AlcanceUsuarioCompania
            {
                UsuarioId = usuarioId,
                CompaniaId = companiaId,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Una compañía inexistente o INACTIVA no puede incorporarse al alcance (RF-032,
    /// contracts/users.yaml 400).
    /// </summary>
    private async Task ValidarCompaniasAsync(IReadOnlyList<Guid> companiaIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(companiaIds);

        var ids = companiaIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var validas = await db.Companias
            .Where(c => ids.Contains(c.Id) && c.Estado == Estado.ACTIVO)
            .Select(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var invalidas = ids.Except(validas).ToList();
        if (invalidas.Count > 0)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValorMaestroInactivo,
                $"Compañías inexistentes o inactivas: {string.Join(", ", invalidas)}.");
        }
    }

    private static UsuarioDto AMapa(Usuario usuario, IReadOnlyList<Guid> alcance) =>
        new(usuario.Id, usuario.Correo, usuario.Estado, usuario.RequiereCambioPassword, alcance);
}
