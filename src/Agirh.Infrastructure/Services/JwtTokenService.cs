using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;

namespace Agirh.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateToken(Employee employee)
    {
        var keyString = _configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(keyString) || keyString.Length < 32)
        {
            _logger.LogCritical("JWT signing key is missing or too short (< 32 chars). Token generation aborted.");
            throw new InvalidOperationException("Configuration JWT invalide: la clé doit faire au moins 32 caractères");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new(ClaimTypes.Email, employee.Email),
            new(ClaimTypes.GivenName, employee.FirstName),
            new(ClaimTypes.Surname, employee.LastName),
            new(ClaimTypes.Role, employee.Role)
        };

        claims.Add(new Claim("is_active", employee.IsActive ? "true" : "false"));

        if (employee.ManagerId.HasValue)
            claims.Add(new Claim("managerId", employee.ManagerId.Value.ToString()));

        var expiryHours = _configuration.GetValue<int>("Jwt:ExpiryHours", 8);
        if (expiryHours < 1) expiryHours = 1;
        if (expiryHours > 24) expiryHours = 24;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "AgirhApi",
            audience: _configuration["Jwt:Audience"] ?? "AgirhClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
