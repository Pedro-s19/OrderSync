using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OrderSync.Application.Seguridad;

namespace OrderSync.Infrastructure.Security;

public sealed class JwtTokenGenerator : ITokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(JwtOptions options)
        => _options = options ?? throw new ArgumentNullException(nameof(options));

    public IssuedToken Generate(TokenUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.ExpirationMinutes);

        var claims = new[]
        {
            new Claim("sub", user.UsuarioId.ToString(CultureInfo.InvariantCulture)),
            new Claim("name", user.Nombre),
            new Claim("email", user.Email),
            new Claim("role", user.RolNombre),
            new Claim("rolId", user.RolId.ToString(CultureInfo.InvariantCulture))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
