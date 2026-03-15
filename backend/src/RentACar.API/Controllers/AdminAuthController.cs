using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Auth;
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
    IRefreshTokenCookieService refreshTokenCookieService) : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequestResponse("Email ve parola zorunludur.");
        }

        var email = request.Email.Trim();
        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        if (adminUser is null || !adminUser.IsActive)
        {
            return UnauthorizedResponse();
        }

        var isPasswordValid = passwordHasher.VerifyPassword(request.Password, adminUser.PasswordHash);
        if (!isPasswordValid)
        {
            return UnauthorizedResponse();
        }

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
            LastSeenAtUtc = DateTime.UtcNow,
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

        return OkResponse(response, "Giri� ba�ar�l�.");
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicyNames.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public IActionResult Me()
    {
        return OkResponse(new
        {
            id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
            role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicyNames.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public IActionResult Logout()
    {
        refreshTokenCookieService.ClearRefreshTokenCookie(HttpContext);
        return OkResponse(new { success = true }, "��k�� ba�ar�l�.");
    }
}
