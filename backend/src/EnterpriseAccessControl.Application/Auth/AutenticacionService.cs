using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>Credenciales inválidas (401). No revela si el correo existe.</summary>
public sealed class CredencialesInvalidasException()
    : ErrorNegocioException("CREDENCIALES_INVALIDAS", "Correo o contraseña incorrectos.", 401);

/// <summary>Usuario INACTIVO o BLOQUEADO (403, Historia 1 criterio 2).</summary>
public sealed class UsuarioNoHabilitadoException(string mensaje)
    : ErrorNegocioException("USUARIO_NO_HABILITADO", mensaje, 403);

/// <summary>
/// Autenticación, bloqueo por intentos fallidos, expiración y cambio de contraseña
/// (RF-001 a RF-003, Historia 1; research.md §2).
/// </summary>
public sealed class AutenticacionService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IRelojSistema reloj,
    PasswordPolicyValidator politicaValidator,
    IOptions<PasswordPolicyOptions> politicaOpciones)
{
    private readonly PasswordPolicyOptions _politica = politicaOpciones.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correo = Usuario.NormalizarCorreo(request.Correo);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.CorreoNormalizado == correo, ct)
            .ConfigureAwait(false);

        // Usuario inexistente y contraseña incorrecta devuelven el mismo error: distinguirlos
        // permitiría enumerar correos registrados.
        if (usuario is null)
        {
            throw new CredencialesInvalidasException();
        }

        // El estado se comprueba ANTES de validar la contraseña: un usuario bloqueado no debe poder
        // seguir consumiendo intentos ni recibir señales distintas según acierte o no.
        if (usuario.Estado == EstadoUsuario.INACTIVO)
        {
            throw new UsuarioNoHabilitadoException("El usuario está inactivo.");
        }

        if (usuario.Estado == EstadoUsuario.BLOQUEADO)
        {
            throw new UsuarioNoHabilitadoException(
                "El usuario está bloqueado por intentos fallidos. Requiere desbloqueo administrativo.");
        }

        if (!hasher.Verificar(usuario.PasswordHash, request.Password))
        {
            usuario.IntentosFallidosConsecutivos++;

            if (usuario.IntentosFallidosConsecutivos >= _politica.IntentosFallidosParaBloqueo)
            {
                usuario.Estado = EstadoUsuario.BLOQUEADO;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new CredencialesInvalidasException();
        }

        // Login correcto: el contador se reinicia para que intentos fallidos aislados en el tiempo
        // no acaben bloqueando a un usuario legítimo.
        usuario.IntentosFallidosConsecutivos = 0;

        // Expiración periódica: no impide entrar, obliga a cambiarla (Historia 1, criterio 3).
        var expirada = usuario.FechaUltimoCambioPassword.AddDays(_politica.DiasExpiracion) <= reloj.UtcNow;
        if (expirada)
        {
            usuario.RequiereCambioPassword = true;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var (rol, companiaIds) = await ResolverAlcanceAsync(usuario.Id, ct).ConfigureAwait(false);
        var token = tokens.Emitir(usuario.Id, usuario.Correo, rol, companiaIds);

        return new LoginResponse(
            token.AccessToken,
            token.ExpiraEn,
            usuario.RequiereCambioPassword,
            rol,
            companiaIds);
    }

    public async Task CambiarPasswordAsync(
        Guid usuarioId,
        CambiarPasswordRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct).ConfigureAwait(false)
            ?? throw new CredencialesInvalidasException();

        if (!hasher.Verificar(usuario.PasswordHash, request.PasswordActual))
        {
            throw new CredencialesInvalidasException();
        }

        var politica = politicaValidator.Validar(request.PasswordNueva);
        if (!politica.EsValida)
        {
            throw new ReglaNegocioInvalidaException(
                "PASSWORD_NO_CUMPLE_POLITICA",
                string.Join(' ', politica.Errores));
        }

        await ValidarNoReutilizadaAsync(usuario, request.PasswordNueva, ct).ConfigureAwait(false);

        // El hash saliente se archiva ANTES de sustituirlo, para que la comprobación de
        // reutilización de la próxima vez lo incluya.
        db.HistorialContrasenas.Add(new HistorialContrasena
        {
            UsuarioId = usuario.Id,
            PasswordHash = usuario.PasswordHash,
        });

        usuario.PasswordHash = hasher.Hash(request.PasswordNueva);
        usuario.FechaUltimoCambioPassword = reloj.UtcNow;
        usuario.RequiereCambioPassword = false;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<SesionActual> ObtenerSesionAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException(
                CodigosError.RecursoNoEncontrado,
                "Usuario no encontrado.");

        var (rol, companiaIds) = await ResolverAlcanceAsync(usuarioId, ct).ConfigureAwait(false);

        return new SesionActual(usuario.Id, usuario.Correo, rol, companiaIds);
    }

    private async Task ValidarNoReutilizadaAsync(Usuario usuario, string passwordNueva, CancellationToken ct)
    {
        // La contraseña vigente cuenta como "ya usada" aunque todavía no esté en el historial.
        if (hasher.Verificar(usuario.PasswordHash, passwordNueva))
        {
            throw new ReglaNegocioInvalidaException(
                "PASSWORD_REUTILIZADA",
                $"No puede reutilizar ninguna de las últimas {_politica.HistorialNoReutilizable} contraseñas.");
        }

        var recientes = await db.HistorialContrasenas
            .Where(h => h.UsuarioId == usuario.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(_politica.HistorialNoReutilizable)
            .Select(h => h.PasswordHash)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Se verifica contra cada hash: los hashes llevan sal propia, así que no se pueden comparar
        // como texto.
        if (recientes.Any(hash => hasher.Verificar(hash, passwordNueva)))
        {
            throw new ReglaNegocioInvalidaException(
                "PASSWORD_REUTILIZADA",
                $"No puede reutilizar ninguna de las últimas {_politica.HistorialNoReutilizable} contraseñas.");
        }
    }

    /// <summary>
    /// Alcance efectivo a partir de las asignaciones de rol vigentes (RF-074, RF-077).
    /// </summary>
    /// <remarks>
    /// <c>GLOBAL_ADMINISTRATOR</c> prevalece sobre cualquier asignación por compañía y no enumera
    /// compañías: su alcance es toda compañía, incluidas las que se creen después de emitido el
    /// token. Un usuario sin ninguna asignación vigente recibe rol <c>null</c> y ninguna compañía;
    /// el gate de autorización lo rechazará en el siguiente request (denegación por defecto).
    /// </remarks>
    private async Task<(RolAdministrativo? Rol, IReadOnlyList<Guid> CompaniaIds)> ResolverAlcanceAsync(
        Guid usuarioId,
        CancellationToken ct)
    {
        var ahora = reloj.UtcNow;

        var vigentes = await db.AsignacionesRolAdministrativo
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId
                        && a.FechaHoraInicio <= ahora
                        && ahora < a.FechaHoraFin)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (vigentes.Count == 0)
        {
            return (null, []);
        }

        if (vigentes.Any(a => a.Rol == RolAdministrativo.GLOBAL_ADMINISTRATOR))
        {
            return (RolAdministrativo.GLOBAL_ADMINISTRATOR, []);
        }

        var companiaIds = vigentes
            .Where(a => a.CompaniaId is not null)
            .Select(a => a.CompaniaId!.Value)
            .Distinct()
            .ToList();

        return (RolAdministrativo.COMPANY_ADMINISTRATOR, companiaIds);
    }
}
