using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RentACar.Core.Entities;

namespace RentACar.API.Services;

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "RentACar.API";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "RentACar.Client";
    private readonly string _secret = configuration["Jwt:Secret"] ?? string.Empty;
    private readonly int _accessTokenHours = ParseAccessTokenHours(configuration["Jwt:AccessTokenHours"]);

    public string CreateAdminAccessToken(AdminUser adminUser, out DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(_secret) || _secret.Length < 32)
        {
            throw new InvalidOperationException("JWT secret must be configured with at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(adminUser.Role))
        {
            throw new InvalidOperationException("Admin user role is required to issue JWT token.");
        }

        expiresAtUtc = DateTime.UtcNow.AddHours(_accessTokenHours);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUser.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, adminUser.Email),
            new(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new(ClaimTypes.Name, adminUser.Email),
            new(ClaimTypes.Role, adminUser.Role),
            new("role", adminUser.Role),
            new("Permission", "admin.access")
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static int ParseAccessTokenHours(string? value)
    {
        if (int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return 24;
    }
}
