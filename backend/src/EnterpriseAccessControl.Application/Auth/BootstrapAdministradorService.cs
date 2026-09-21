using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnterpriseAccessControl.Application.Auth;

/// <summary>Resultado del intento de siembra del administrador inicial (RF-078).</summary>
public enum ResultadoBootstrap
{
    /// <summary>Se creó el primer administrador global.</summary>
    Creado = 1,

    /// <summary>Ya existía una asignación GLOBAL_ADMINISTRATOR: no se hizo nada.</summary>
    YaExistia = 2,
}

/// <summary>
/// Crea el primer <c>GLOBAL_ADMINISTRATOR</c> en el arranque, de forma idempotente (RF-078).
/// </summary>
/// <remarks>
/// **Por qué no es una migración de datos de EF Core.** Una migración no lee <c>IOptions</c> ni la
/// configuración de forma natural, y la contraseña debe venir de un secreto: incrustarla en una
/// migración la dejaría versionada en el repositorio. Por eso es una rutina de arranque de la
/// aplicación, ejecutada **después** de aplicar migraciones.
///
/// **Por qué no pasa por el pipeline de autorización.** En el arranque no hay solicitante
/// autenticado al que aplicarle RF-076, ni <c>HttpContext</c> del que derivar claims. La rutina
/// escribe directamente por persistencia y reutiliza
/// <see cref="AsignacionRolAdministrativoService"/> solo para que la asignación sembrada cumpla los
/// mismos invariantes de dominio que cualquier otra.
///
/// **Idempotencia**: la condición es la ausencia de *cualquier* asignación
/// <c>GLOBAL_ADMINISTRATOR</c>, no la del usuario del bootstrap. Reiniciar la aplicación no crea un
/// segundo administrador, y tampoco lo hace si el administrador inicial fue renombrado o sustituido
/// por otro global.
/// </remarks>
public sealed class BootstrapAdministradorService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IRelojSistema reloj,
    PasswordPolicyValidator politicaValidator,
    AsignacionRolAdministrativoService asignaciones,
    IOptions<BootstrapOptions> opciones)
{
    private readonly BootstrapOptions _bootstrap = opciones.Value;

    public async Task<ResultadoBootstrap> EjecutarAsync(CancellationToken ct = default)
    {
        var yaHayGlobal = await db.AsignacionesRolAdministrativo
            .AnyAsync(a => a.Rol == RolAdministrativo.GLOBAL_ADMINISTRATOR, ct)
            .ConfigureAwait(false);

        if (yaHayGlobal)
        {
            return ResultadoBootstrap.YaExistia;
        }

        var correo = _bootstrap.AdminEmail.Trim();
        var correoNormalizado = Usuario.NormalizarCorreo(correo);

        // La contraseña sembrada cumple la política vigente sin excepción (RF-078, D9 #1): no hay
        // umbrales especiales para el usuario de arranque.
        var politica = politicaValidator.Validar(_bootstrap.AdminPassword);

        if (!politica.EsValida)
        {
            throw new InvalidOperationException(
                "La contraseña de arranque no cumple la política de contraseñas vigente: "
                + string.Join(' ', politica.Errores));
        }

        var ahora = reloj.UtcNow;

        // Puede existir ya el usuario sin ser administrador global (p. ej. creado por otro global
        // que luego expiró). En ese caso se le añade la asignación en vez de duplicar el correo,
        // que es único en todo el sistema (RF-001).
        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.CorreoNormalizado == correoNormalizado, ct)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Correo = correo,
                PasswordHash = hasher.Hash(_bootstrap.AdminPassword),
                Estado = EstadoUsuario.ACTIVO,
                // Obligatorio: la contraseña semilla la conoce quien desplegó (RF-078).
                RequiereCambioPassword = true,
                FechaUltimoCambioPassword = ahora,
            };

            db.Usuarios.Add(usuario);
        }

        var asignacion = await asignaciones
            .ConstruirValidadaAsync(
                usuario.Id,
                new AsignarRolRequest(
                    RolAdministrativo.GLOBAL_ADMINISTRATOR,
                    CompaniaId: null,
                    FechaHoraInicio: ahora,
                    // Única excepción del sistema a RF-071/RF-075, acotada a esta fila (RF-078).
                    FechaHoraFin: VigenciaBootstrap.MaxValidityDate),
                ct)
            .ConfigureAwait(false);

        db.AsignacionesRolAdministrativo.Add(asignacion);

        // Sin usuario autenticado, la auditoría registra null en CreatedById/UpdatedById: lo creó el
        // sistema, y inventar un identificador falsearía la trazabilidad (Principio III).
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return ResultadoBootstrap.Creado;
    }
}
