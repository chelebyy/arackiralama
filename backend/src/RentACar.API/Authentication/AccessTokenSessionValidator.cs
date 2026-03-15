using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Authentication;

public sealed class AccessTokenSessionValidator(
    IApplicationDbContext dbContext,
    ILogger<AccessTokenSessionValidator> logger) : IAccessTokenSessionValidator
{
    public async Task<AccessTokenSessionValidationFailure> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!TryReadClaims(principal, out var claims))
        {
            logger.LogWarning("Access token session validation failed. reason={Reason}", AccessTokenSessionValidationFailure.MissingRequiredClaims);
            return AccessTokenSessionValidationFailure.MissingRequiredClaims;
        }

        if (claims is null)
        {
            logger.LogWarning("Access token session validation failed. reason={Reason}", AccessTokenSessionValidationFailure.InvalidClaimFormat);
            return AccessTokenSessionValidationFailure.InvalidClaimFormat;
        }

        var authSession = await dbContext.AuthSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == claims.SessionId, cancellationToken);

        if (authSession is null ||
            authSession.PrincipalId != claims.PrincipalId ||
            authSession.PrincipalType != claims.PrincipalType)
        {
            LogFailure(AccessTokenSessionValidationFailure.SessionNotFound, claims);
            return AccessTokenSessionValidationFailure.SessionNotFound;
        }

        if (authSession.RevokedAtUtc.HasValue)
        {
            LogFailure(AccessTokenSessionValidationFailure.SessionRevoked, claims);
            return AccessTokenSessionValidationFailure.SessionRevoked;
        }

        if (authSession.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
        {
            LogFailure(AccessTokenSessionValidationFailure.SessionExpired, claims);
            return AccessTokenSessionValidationFailure.SessionExpired;
        }

        var currentTokenVersion = await GetCurrentTokenVersionAsync(claims.PrincipalType, claims.PrincipalId, cancellationToken);
        if (currentTokenVersion is null)
        {
            LogFailure(AccessTokenSessionValidationFailure.PrincipalNotFound, claims);
            return AccessTokenSessionValidationFailure.PrincipalNotFound;
        }

        if (currentTokenVersion.Value != claims.TokenVersion)
        {
            LogFailure(AccessTokenSessionValidationFailure.TokenVersionMismatch, claims);
            return AccessTokenSessionValidationFailure.TokenVersionMismatch;
        }

        return AccessTokenSessionValidationFailure.None;
    }

    private async Task<int?> GetCurrentTokenVersionAsync(
        AuthPrincipalType principalType,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        return principalType switch
        {
            AuthPrincipalType.Admin => await dbContext.AdminUsers
                .AsNoTracking()
                .Where(admin => admin.Id == principalId)
                .Select(admin => (int?)admin.TokenVersion)
                .FirstOrDefaultAsync(cancellationToken),
            AuthPrincipalType.Customer => await dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.Id == principalId)
                .Select(customer => (int?)customer.TokenVersion)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    private bool TryReadClaims(ClaimsPrincipal principal, out ParsedSessionClaims? claims)
    {
        claims = null;

        var principalTypeValue = principal.FindFirst(AuthClaimTypes.PrincipalType)?.Value;
        var principalIdValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sessionIdValue = principal.FindFirst(JwtRegisteredClaimNames.Sid)?.Value
            ?? principal.FindFirst(ClaimTypes.Sid)?.Value;
        var tokenVersionValue = principal.FindFirst(AuthClaimTypes.TokenVersion)?.Value;

        if (string.IsNullOrWhiteSpace(principalTypeValue) ||
            string.IsNullOrWhiteSpace(principalIdValue) ||
            string.IsNullOrWhiteSpace(sessionIdValue) ||
            string.IsNullOrWhiteSpace(tokenVersionValue))
        {
            return false;
        }

        if (!Enum.TryParse<AuthPrincipalType>(principalTypeValue, ignoreCase: true, out var principalType) ||
            !Guid.TryParse(principalIdValue, out var principalId) ||
            !Guid.TryParse(sessionIdValue, out var sessionId) ||
            !int.TryParse(tokenVersionValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokenVersion))
        {
            claims = null;
            return true;
        }

        claims = new ParsedSessionClaims(principalType, principalId, sessionId, tokenVersion);
        return true;
    }

    private void LogFailure(AccessTokenSessionValidationFailure failure, ParsedSessionClaims claims)
    {
        logger.LogWarning(
            "Access token session validation failed. reason={Reason} principal_type={PrincipalType} principal_id={PrincipalId} session_id={SessionId} token_version={TokenVersion}",
            failure,
            claims.PrincipalType,
            claims.PrincipalId,
            claims.SessionId,
            claims.TokenVersion);
    }

    private sealed record ParsedSessionClaims(
        AuthPrincipalType PrincipalType,
        Guid PrincipalId,
        Guid SessionId,
        int TokenVersion);
}
