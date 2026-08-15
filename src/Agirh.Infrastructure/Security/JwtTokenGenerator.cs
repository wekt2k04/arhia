using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Agirh.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Agirh.Infrastructure.Security;

public sealed class JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int DureeValiditeMinutes { get; init; } = 60;
}

public class JwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(JwtOptions options)
    {
        _options = options;
    }

    public string GenererToken(CompteUtilisateur compte, DateTime maintenant)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, compte.Id.ToString()),
            new(ClaimTypes.Email, compte.Email),
            new(ClaimTypes.Role, compte.Role.ToString())
        };

        if (compte.PoleId is not null)
            claims.Add(new Claim("poleId", compte.PoleId.Value.ToString()));

        var cle = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(cle, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: maintenant,
            expires: maintenant.AddMinutes(_options.DureeValiditeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
