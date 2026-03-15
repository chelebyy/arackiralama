using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RentACar.API.Authentication;
using RentACar.API.Options;
using RentACar.Core.Entities;
using RentACar.Core.Enums;

namespace RentACar.API.Services;

public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private const int DefaultAccessTokenMinutes = 15;
    private const int DefaultRefreshTokenDays = 7;
    private const int RefreshTokenEntropyBytes = 64;
    private const string HashPrefix = "sha256:";

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public string CreateAdminAccessToken(AdminUser adminUser, Guid sessionId, out DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(adminUser.Role))
        {
            throw new InvalidOperationException("Admin user role is required to issue JWT token.");
        }

        if (!AuthRoleNames.IsAdminRole(adminUser.Role))
        {
            throw new InvalidOperationException("Admin user role must be either Admin or SuperAdmin to issue JWT token.");
        }

        var principalClaims = new JwtPrincipalClaims(
            PrincipalId: adminUser.Id,
            Email: adminUser.Email,
            PrincipalType: AuthPrincipalType.Admin,
            Role: adminUser.Role,
            TokenVersion: adminUser.TokenVersion,
            SessionId: sessionId,
            AdditionalClaims: [new Claim(AuthClaimTypes.Permission, AuthPermissionNames.AdminAccess)]);

        return CreateAccessToken(principalClaims, out expiresAtUtc);
    }

    public string CreateCustomerAccessToken(Customer customer, Guid sessionId, out DateTime expiresAtUtc)
    {
        var principalClaims = new JwtPrincipalClaims(
            PrincipalId: customer.Id,
            Email: customer.Email,
            PrincipalType: AuthPrincipalType.Customer,
            Role: AuthRoleNames.Customer,
            TokenVersion: customer.TokenVersion,
            SessionId: sessionId);

        return CreateAccessToken(principalClaims, out expiresAtUtc);
    }

    public string CreateRefreshToken(out DateTime expiresAtUtc)
    {
        var refreshTokenDays = _jwtOptions.RefreshTokenDays > 0
            ? _jwtOptions.RefreshTokenDays
            : DefaultRefreshTokenDays;

        expiresAtUtc = DateTime.UtcNow.AddDays(refreshTokenDays);

        var tokenBytes = RandomNumberGenerator.GetBytes(RefreshTokenEntropyBytes);
        return Base64UrlEncoder.Encode(tokenBytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token cannot be null or empty.", nameof(refreshToken));
        }

        var normalizedToken = refreshToken.Trim();
        var tokenBytes = Encoding.UTF8.GetBytes(normalizedToken);
        var hashBytes = SHA256.HashData(tokenBytes);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"{HashPrefix}{hashHex}";
    }

    public bool VerifyRefreshToken(string refreshToken, string refreshTokenHash)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            return false;
        }

        var expectedHash = HashRefreshToken(refreshToken);
        var normalizedProvidedHash = NormalizeRefreshTokenHash(refreshTokenHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(normalizedProvidedHash));
    }

    public bool IsRefreshTokenReplay(string refreshToken, string activeRefreshTokenHash) =>
        !VerifyRefreshToken(refreshToken, activeRefreshTokenHash);

    private static string NormalizeRefreshTokenHash(string refreshTokenHash)
    {
        var hash = refreshTokenHash.Trim().ToLowerInvariant();
        return hash.StartsWith(HashPrefix, StringComparison.Ordinal)
            ? hash
            : $"{HashPrefix}{hash}";
    }

    private string CreateAccessToken(JwtPrincipalClaims principalClaims, out DateTime expiresAtUtc)
    {
        EnsureJwtOptions();

        if (string.IsNullOrWhiteSpace(principalClaims.Email))
        {
            throw new InvalidOperationException("Principal email is required to issue JWT token.");
        }

        if (principalClaims.SessionId == Guid.Empty)
        {
            throw new InvalidOperationException("Session id is required to issue JWT token.");
        }

        var accessTokenMinutes = _jwtOptions.AccessTokenMinutes > 0
            ? _jwtOptions.AccessTokenMinutes
            : DefaultAccessTokenMinutes;

        expiresAtUtc = DateTime.UtcNow.AddMinutes(accessTokenMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, principalClaims.PrincipalId.ToString()),
            new(JwtRegisteredClaimNames.Email, principalClaims.Email),
            new(ClaimTypes.NameIdentifier, principalClaims.PrincipalId.ToString()),
            new(ClaimTypes.Name, principalClaims.Email),
            new(AuthClaimTypes.PrincipalType, principalClaims.PrincipalType.ToString()),
            new(JwtRegisteredClaimNames.Sid, principalClaims.SessionId.ToString()),
            new(AuthClaimTypes.TokenVersion, principalClaims.TokenVersion.ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(principalClaims.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, principalClaims.Role));
            claims.Add(new Claim(AuthClaimTypes.Role, principalClaims.Role));
        }

        if (principalClaims.AdditionalClaims is not null)
        {
            claims.AddRange(principalClaims.AdditionalClaims);
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void EnsureJwtOptions()
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.Secret) || _jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT secret must be configured with at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer))
        {
            throw new InvalidOperationException("JWT issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.Audience))
        {
            throw new InvalidOperationException("JWT audience must be configured.");
        }
    }
}
