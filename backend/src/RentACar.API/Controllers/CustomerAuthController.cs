using System.IdentityModel.Tokens.Jwt;
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

[Route("api/customer/v1/auth")]
public sealed class CustomerAuthController(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenCookieService refreshTokenCookieService,
    IOptions<RefreshTokenCookieSettings> refreshTokenCookieSettings) : BaseApiController
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly string _refreshTokenCookieName = refreshTokenCookieSettings.Value.Name;

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Register([FromBody] CustomerRegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequestResponse("Email ve parola zorunludur.");
        }

        var email = request.Email.Trim();
        var normalizedEmail = Customer.NormalizeEmail(email);
        var existingCustomer = await dbContext.Customers
            .FirstOrDefaultAsync(customer => customer.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingCustomer is not null && existingCustomer.HasPassword)
        {
            return UnauthorizedResponse();
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);

        if (existingCustomer is not null)
        {
            existingCustomer.Email = email;
            existingCustomer.PasswordHash = passwordHash;
            ApplyProfileUpdates(existingCustomer, request);
        }
        else
        {
            var newCustomer = new Customer
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = request.FullName?.Trim() ?? string.Empty,
                Phone = request.Phone?.Trim() ?? string.Empty,
                IdentityNumber = string.Empty,
                Nationality = "TR",
                LicenseYear = 0
            };

            dbContext.Customers.Add(newCustomer);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return OkResponse(new { success = true }, "Kayit basarili.");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Login([FromBody] CustomerLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return UnauthorizedResponse();
        }

        var normalizedEmail = Customer.NormalizeEmail(request.Email);
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(existingCustomer => existingCustomer.NormalizedEmail == normalizedEmail, cancellationToken);

        if (customer is null || !customer.HasPassword)
        {
            return UnauthorizedResponse();
        }

        var utcNow = DateTime.UtcNow;

        if (customer.LockoutEndUtc.HasValue)
        {
            if (customer.LockoutEndUtc.Value > utcNow)
            {
                return UnauthorizedResponse();
            }

            customer.LockoutEndUtc = null;
            customer.FailedLoginCount = 0;
        }

        var isPasswordValid = passwordHasher.VerifyPassword(request.Password, customer.PasswordHash!);
        if (!isPasswordValid)
        {
            customer.FailedLoginCount += 1;

            if (customer.FailedLoginCount >= LockoutThreshold)
            {
                customer.LockoutEndUtc = utcNow.Add(LockoutDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return UnauthorizedResponse();
        }

        customer.FailedLoginCount = 0;
        customer.LockoutEndUtc = null;
        customer.LastLoginAtUtc = utcNow;

        var sessionId = Guid.NewGuid();
        var accessToken = jwtTokenService.CreateCustomerAccessToken(customer, sessionId, out var expiresAtUtc);
        var refreshToken = jwtTokenService.CreateRefreshToken(out var refreshTokenExpiresAtUtc);
        var refreshTokenHash = jwtTokenService.HashRefreshToken(refreshToken);

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = sessionId,
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
            RefreshTokenHash = refreshTokenHash,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            LastSeenAtUtc = utcNow,
            CreatedByIp = ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ControllerContext.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        refreshTokenCookieService.AppendRefreshTokenCookie(HttpContext, refreshToken, refreshTokenExpiresAtUtc);

        var response = new CustomerAuthResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expiresAtUtc,
            CustomerId: customer.Id,
            Email: customer.Email,
            FullName: customer.FullName);

        return OkResponse(response, "Giris basarili.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!TryReadRefreshToken(out var refreshToken))
        {
            return UnauthorizedResponse();
        }

        var refreshTokenHash = jwtTokenService.HashRefreshToken(refreshToken);
        var utcNow = DateTime.UtcNow;

        var currentSession = await dbContext.AuthSessions
            .FirstOrDefaultAsync(session =>
                session.PrincipalType == AuthPrincipalType.Customer &&
                session.RefreshTokenHash == refreshTokenHash,
                cancellationToken);

        if (currentSession is null ||
            currentSession.RevokedAtUtc.HasValue ||
            currentSession.ReplacedBySessionId.HasValue ||
            currentSession.RefreshTokenExpiresAtUtc <= utcNow)
        {
            return UnauthorizedResponse();
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(existingCustomer => existingCustomer.Id == currentSession.PrincipalId, cancellationToken);

        if (customer is null || !customer.HasPassword)
        {
            return UnauthorizedResponse();
        }

        var newSessionId = Guid.NewGuid();
        var accessToken = jwtTokenService.CreateCustomerAccessToken(customer, newSessionId, out var accessTokenExpiresAtUtc);
        var newRefreshToken = jwtTokenService.CreateRefreshToken(out var refreshTokenExpiresAtUtc);
        var newRefreshTokenHash = jwtTokenService.HashRefreshToken(newRefreshToken);

        currentSession.RevokedAtUtc = utcNow;
        currentSession.ReplacedBySessionId = newSessionId;
        currentSession.LastSeenAtUtc = utcNow;

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = newSessionId,
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
            RefreshTokenHash = newRefreshTokenHash,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            LastSeenAtUtc = utcNow,
            CreatedByIp = ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ControllerContext.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        refreshTokenCookieService.AppendRefreshTokenCookie(HttpContext, newRefreshToken, refreshTokenExpiresAtUtc);

        var response = new CustomerRefreshResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: accessTokenExpiresAtUtc);

        return OkResponse(response, "Oturum yenilendi.");
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicyNames.CustomerOnly)]
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
                existingSession.PrincipalType == AuthPrincipalType.Customer &&
                existingSession.PrincipalId == principalId,
                cancellationToken);

        if (session is not null && !session.RevokedAtUtc.HasValue)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            session.LastSeenAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        refreshTokenCookieService.ClearRefreshTokenCookie(HttpContext);

        return OkResponse(new { success = true }, "Cikis basarili.");
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicyNames.CustomerOnly)]
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

    private bool TryReadRefreshToken(out string refreshToken)
    {
        refreshToken = string.Empty;

        if (!HttpContext.Request.Cookies.TryGetValue(_refreshTokenCookieName, out var cookieRefreshToken) ||
            string.IsNullOrWhiteSpace(cookieRefreshToken))
        {
            return false;
        }

        refreshToken = cookieRefreshToken.Trim();
        return true;
    }

    private bool TryReadSessionContext(out Guid principalId, out Guid sessionId)
    {
        principalId = Guid.Empty;
        sessionId = Guid.Empty;

        var principalIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sessionIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value
            ?? User.FindFirst(ClaimTypes.Sid)?.Value;

        return Guid.TryParse(principalIdClaim, out principalId) &&
               Guid.TryParse(sessionIdClaim, out sessionId);
    }

    private static void ApplyProfileUpdates(Customer customer, CustomerRegisterRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            customer.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            customer.Phone = request.Phone.Trim();
        }
    }
}
