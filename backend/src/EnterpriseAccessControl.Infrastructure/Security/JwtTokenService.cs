using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Options;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseAccessControl.Infrastructure.Security;

/// <summary>
/// Emite el JWT de acceso con el alcance administrativo del usuario (research.md §2, §3, §27).
/// </summary>
public sealed class JwtTokenService(
    IOptions<JwtOptions> opciones,
    IRelojSistema reloj) : ITokenService
{
    private readonly JwtOptions _jwt = opciones.Value;

    public TokenEmitido Emitir(
        Guid usuarioId,
        string correo,
        RolAdministrativo? rol,
        IReadOnlyList<Guid> companiaIds)
    {
        ArgumentNullException.ThrowIfNull(companiaIds);

        var emitidoEn = reloj.UtcNow;
        var expiraEn = emitidoEn.AddMinutes(_jwt.AccessTokenMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new(JwtRegisteredClaimNames.Email, correo),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        if (rol is not null)
        {
            claims.Add(new Claim(ClaimsPersonalizados.Rol, rol.Value.ToString()));
        }

        // Un claim por compañía, solo para COMPANY_ADMINISTRATOR: el alcance GLOBAL lo expresa el
        // claim de rol y enumerarlo sería a la vez imposible de mantener y falso en cuanto se creara
        // una compañía nueva (RF-074).
        if (rol == RolAdministrativo.COMPANY_ADMINISTRATOR)
        {
            claims.AddRange(
                companiaIds.Select(id => new Claim(ClaimsPersonalizados.AlcanceCompania, id.ToString())));
        }

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: emitidoEn,
            expires: expiraEn,
            signingCredentials: credenciales);

        var serializado = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenEmitido(serializado, expiraEn);
    }
}
