using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Auth;
using RentACar.API.Options;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/auth")]
public sealed class AdminAuthController(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenCookieService refreshTokenCookieService,
    IOptions<RefreshTokenCookieSettings> refreshTokenCookieSettings) : BaseApiController
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly string _refreshTokenCookieName = refreshTokenCookieSettings.Value.Name;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequestResponse("Email ve parola zorunludur.");
        }

        var normalizedEmail = AdminUser.NormalizeEmail(request.Email);
        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (adminUser is null || !adminUser.IsActive)
        {
            return UnauthorizedResponse();
        }

        var utcNow = DateTime.UtcNow;

        if (adminUser.LockoutEndUtc.HasValue)
        {
            if (adminUser.LockoutEndUtc.Value > utcNow)
            {
                return UnauthorizedResponse();
            }

            adminUser.LockoutEndUtc = null;
            adminUser.FailedLoginCount = 0;
        }

        var isPasswordValid = passwordHasher.VerifyPassword(request.Password, adminUser.PasswordHash);
        if (!isPasswordValid)
        {
            adminUser.FailedLoginCount += 1;

            if (adminUser.FailedLoginCount >= LockoutThreshold)
            {
                adminUser.LockoutEndUtc = utcNow.Add(LockoutDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return UnauthorizedResponse();
        }

        adminUser.FailedLoginCount = 0;
        adminUser.LockoutEndUtc = null;
        adminUser.LastLoginAtUtc = utcNow;

        var sessionId = Guid.NewGuid();
        var accessToken = jwtTokenService.CreateAdminAccessToken(adminUser, sessionId, out var expiresAtUtc);
        var refreshToken = jwtTokenService.CreateRefreshToken(out var refreshTokenExpiresAtUtc);
        var refreshTokenHash = jwtTokenService.HashRefreshToken(refreshToken);

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = sessionId,
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = refreshTokenHash,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            LastSeenAtUtc = utcNow,
            CreatedByIp = ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ControllerContext.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        refreshTokenCookieService.AppendRefreshTokenCookie(HttpContext, refreshToken, refreshTokenExpiresAtUtc);

        var response = new AdminLoginResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expiresAtUtc,
            Role: adminUser.Role,
            FullName: adminUser.FullName,
            Email: adminUser.Email);

        return OkResponse(response, "Giriş başarılı.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!TryReadRefreshToken(_refreshTokenCookieName, out var refreshToken))
        {
            return UnauthorizedResponse();
        }

        var refreshTokenHash = jwtTokenService.HashRefreshToken(refreshToken);
        var utcNow = DateTime.UtcNow;

        var currentSession = await dbContext.AuthSessions
            .FirstOrDefaultAsync(session =>
                session.PrincipalType == AuthPrincipalType.Admin &&
                session.RefreshTokenHash == refreshTokenHash,
                cancellationToken);

        if (currentSession is null ||
            currentSession.RevokedAtUtc.HasValue ||
            currentSession.ReplacedBySessionId.HasValue ||
            currentSession.RefreshTokenExpiresAtUtc <= utcNow)
        {
            return UnauthorizedResponse();
        }

        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.Id == currentSession.PrincipalId, cancellationToken);

        if (adminUser is null || !adminUser.IsActive)
        {
            return UnauthorizedResponse();
        }

        var newSessionId = Guid.NewGuid();
        var accessToken = jwtTokenService.CreateAdminAccessToken(adminUser, newSessionId, out var accessTokenExpiresAtUtc);
        var newRefreshToken = jwtTokenService.CreateRefreshToken(out var refreshTokenExpiresAtUtc);
        var newRefreshTokenHash = jwtTokenService.HashRefreshToken(newRefreshToken);

        currentSession.RevokedAtUtc = utcNow;
        currentSession.ReplacedBySessionId = newSessionId;
        currentSession.LastSeenAtUtc = utcNow;

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = newSessionId,
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = newRefreshTokenHash,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            LastSeenAtUtc = utcNow,
            CreatedByIp = ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ControllerContext.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        refreshTokenCookieService.AppendRefreshTokenCookie(HttpContext, newRefreshToken, refreshTokenExpiresAtUtc);

        var response = new AdminRefreshResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: accessTokenExpiresAtUtc);

        return OkResponse(response, "Oturum yenilendi.");
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicyNames.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public IActionResult Me()
    {
        return OkResponse(new
        {
            id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            email = User.FindFirst(ClaimTypes.Name)?.Value,
            role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicyNames.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!TryReadSessionContext(out var principalId, out var sessionId))
        {
            refreshTokenCookieService.ClearRefreshTokenCookie(HttpContext);
            return UnauthorizedResponse();
        }

        var session = await dbContext.AuthSessions
            .FirstOrDefaultAsync(existingSession =>
                existingSession.Id == sessionId &&
                existingSession.PrincipalType == AuthPrincipalType.Admin &&
                existingSession.PrincipalId == principalId,
                cancellationToken);

        if (session is null)
        {
            refreshTokenCookieService.ClearRefreshTokenCookie(HttpContext);
            return UnauthorizedResponse();
        }

        if (!session.RevokedAtUtc.HasValue)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            session.LastSeenAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        refreshTokenCookieService.ClearRefreshTokenCookie(HttpContext);

        return OkResponse(new { success = true }, "Çıkış başarılı.");
    }

}
