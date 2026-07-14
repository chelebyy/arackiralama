using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Auth;
using RentACar.API.Options;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Services;
using RentACar.Infrastructure.Services.Notifications;

namespace RentACar.API.Controllers;

[Route("api/customer/v1/auth")]
public sealed class CustomerAuthController(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenCookieService refreshTokenCookieService,
    IAuditLogService auditLogService,
    IOptions<RefreshTokenCookieSettings> refreshTokenCookieSettings,
    ICustomerAccountClaimEmailDispatcher accountClaimEmailDispatcher,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<AccountClaimSecurityOptions> accountClaimSecurityOptions,
    TimeProvider timeProvider,
    ILogger<CustomerAuthController> logger) : BaseApiController
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ClaimTokenLifetime = TimeSpan.FromHours(1);
    private static readonly string[] SupportedLocales = ["tr-TR", "en-US", "ru-RU", "ar-SA", "de-DE"];

    private readonly string _refreshTokenCookieName = refreshTokenCookieSettings.Value.Name;
    private readonly ICustomerAccountClaimEmailDispatcher _accountClaimEmailDispatcher = accountClaimEmailDispatcher;
    private readonly string _defaultLocale = string.IsNullOrWhiteSpace(notificationOptions.Value.DefaultLocale)
        ? "tr-TR"
        : notificationOptions.Value.DefaultLocale;
    private readonly ILogger<CustomerAuthController> _logger = logger;
    private readonly TimeSpan _claimRequestCooldown = TimeSpan.FromMinutes(accountClaimSecurityOptions.Value.RequestCooldownMinutes);

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
            await auditLogService.LogAsync(
                "RegisterIgnored",
                "CustomerAuth",
                normalizedEmail,
                null,
                null,
                null,
                ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return OkResponse(new { success = true }, "Kayit basarili.");
        }

        if (existingCustomer is not null && !existingCustomer.HasPassword)
        {
            // Account claim path: knowledge of an email is NOT sufficient to install
            // credentials on an existing passwordless customer. Issue a single-use
            // claim token and dispatch a verification email. The customer record is
            // left untouched and the request body fields are ignored.
            var claimIssued = await CreateAndDispatchClaimTokenAsync(
                existingCustomer.Id,
                existingCustomer.Email,
                cancellationToken);

            await auditLogService.LogAsync(
                claimIssued ? "CustomerClaimRequested" : "CustomerClaimSuppressed",
                "CustomerAuth",
                existingCustomer.Id.ToString(),
                null,
                null,
                null,
                ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return OkResponse(new { success = true }, "Kayit basarili.");
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);
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

        await dbContext.SaveChangesAsync(cancellationToken);

        var persistedCustomer = dbContext.Customers.Local.First(customer => customer.NormalizedEmail == normalizedEmail);
        await auditLogService.LogAsync(
            "Register",
            "CustomerAuth",
            persistedCustomer.Id.ToString(),
            null,
            null,
            null,
            ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return OkResponse(new { success = true }, "Kayit basarili.");
    }

    [HttpPost("claim")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Claim([FromBody] CustomerAccountClaimRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequestResponse("Token ve yeni parola zorunludur.");
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = jwtTokenService.HashRefreshToken(request.Token);

        var claimToken = await dbContext.CustomerAccountClaimTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (claimToken is null)
        {
            await auditLogService.LogAsync(
                "CustomerClaimRejected",
                "CustomerAuth",
                claimToken?.CustomerId.ToString() ?? "unknown",
                null,
                null,
                null,
                ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return BadRequestResponse("Gecersiz veya suresi dolmus dogrulama baglantisi.");
        }

        var transaction = await TryBeginTransactionAsync(cancellationToken);
        try
        {
            var consumedCount = transaction is null
                ? await ConsumeActiveClaimTokenWithNonRelationalProviderAsync(claimToken.Id, utcNow, cancellationToken)
                : await dbContext.CustomerAccountClaimTokens
                    .Where(token =>
                        token.Id == claimToken.Id
                        && token.TokenHash == tokenHash
                        && !token.ConsumedAtUtc.HasValue
                        && !token.SupersededAtUtc.HasValue
                        && token.ExpiresAtUtc > utcNow)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(token => token.ConsumedAtUtc, utcNow),
                        cancellationToken);

            if (consumedCount != 1)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                await auditLogService.LogAsync(
                    "CustomerClaimRejected",
                    "CustomerAuth",
                    claimToken.CustomerId.ToString(),
                    null,
                    null,
                    null,
                    ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken);

                return BadRequestResponse("Gecersiz veya suresi dolmus dogrulama baglantisi.");
            }

            var customer = await dbContext.Customers
                .FirstOrDefaultAsync(existingCustomer => existingCustomer.Id == claimToken.CustomerId, cancellationToken);

            if (customer is null || customer.HasPassword)
            {
                // Reject claims against customers that no longer exist or already have a
                // password, preventing a previously stolen token from overwriting a real
                // account's credentials.
                if (transaction is null)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    await transaction.CommitAsync(cancellationToken);
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                await auditLogService.LogAsync(
                    "CustomerClaimRejected",
                    "CustomerAuth",
                    claimToken.CustomerId.ToString(),
                    null,
                    null,
                    null,
                    ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken);

                return BadRequestResponse("Gecersiz veya suresi dolmus dogrulama baglantisi.");
            }

            customer.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            customer.TokenVersion += 1;

            await SupersedeOtherActiveClaimTokensAsync(customer.Id, claimToken.Id, utcNow, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                await transaction.DisposeAsync();
                transaction = null;
            }

            await auditLogService.LogAsync(
                "CustomerClaimCompleted",
                "CustomerAuth",
                customer.Id.ToString(),
                null,
                null,
                null,
                ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return OkResponse(new { success = true }, "Hesap sahiplenmesi tamamlandi.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
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
        if (!TryReadRefreshToken(_refreshTokenCookieName, out var refreshToken))
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
    [DisableRateLimiting]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(out var principalId))
        {
            return UnauthorizedResponse();
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == principalId, cancellationToken);

        if (customer is null)
        {
            return NotFoundResponse("Kullanici bulunamadi.");
        }

        var response = new CustomerProfileResponse(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.Phone,
            customer.IdentityNumber,
            customer.Nationality,
            customer.LicenseYear,
            customer.BirthDate);

        return OkResponse(response);
    }

    [HttpPut("profile")]
    [Authorize(Policy = AuthPolicyNames.CustomerOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(out var principalId))
        {
            return UnauthorizedResponse();
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == principalId, cancellationToken);

        if (customer is null)
        {
            return NotFoundResponse("Kullanici bulunamadi.");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            customer.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            customer.Phone = request.Phone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.IdentityNumber))
        {
            customer.IdentityNumber = request.IdentityNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Nationality))
        {
            customer.Nationality = request.Nationality.Trim();
        }

        if (request.LicenseYear.HasValue && request.LicenseYear.Value >= 1900)
        {
            customer.LicenseYear = request.LicenseYear.Value;
        }

        if (request.BirthDate.HasValue)
        {
            customer.BirthDate = request.BirthDate.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new CustomerProfileResponse(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.Phone,
            customer.IdentityNumber,
            customer.Nationality,
            customer.LicenseYear,
            customer.BirthDate);

        return OkResponse(response, "Profil guncellendi.");
    }

    private bool TryReadPrincipalId(out Guid principalId)
    {
        principalId = Guid.Empty;

        var principalIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(principalIdClaim, out principalId);
    }

    private async Task<bool> CreateAndDispatchClaimTokenAsync(
        Guid customerId,
        string destinationEmail,
        CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var cooldownCutoff = utcNow.Subtract(_claimRequestCooldown);
        var recentlyIssued = await dbContext.CustomerAccountClaimTokens
            .AsNoTracking()
            .AnyAsync(token => token.CustomerId == customerId && token.CreatedAt >= cooldownCutoff, cancellationToken);
        if (recentlyIssued)
        {
            _logger.LogInformation("Customer account claim request suppressed by cooldown.");
            return false;
        }

        var rawToken = GenerateClaimToken();
        var tokenHash = jwtTokenService.HashRefreshToken(rawToken);
        var expiresAtUtc = utcNow.Add(ClaimTokenLifetime);
        await using var transaction = await TryBeginTransactionAsync(cancellationToken);

        try
        {
            var activeTokens = await dbContext.CustomerAccountClaimTokens
                .Where(token =>
                    token.CustomerId == customerId
                    && !token.ConsumedAtUtc.HasValue
                    && !token.SupersededAtUtc.HasValue)
                .ToListAsync(cancellationToken);

            foreach (var activeToken in activeTokens)
            {
                activeToken.SupersededAtUtc = utcNow;
            }

            if (transaction is not null && activeTokens.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            dbContext.CustomerAccountClaimTokens.Add(new CustomerAccountClaimToken
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                TokenHash = tokenHash,
                ExpiresAtUtc = expiresAtUtc,
                IssuedFromIp = ControllerContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                IssuedUserAgent = Truncate(ControllerContext.HttpContext?.Request.Headers.UserAgent.ToString(), 512)
            });

            await _accountClaimEmailDispatcher.DispatchAsync(
                destinationEmail,
                rawToken,
                expiresAtUtc,
                ResolveLocaleFromRequest(),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_customer_account_claim_tokens_one_active"
            })
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _logger.LogInformation("Customer account claim request suppressed by concurrent issuance.");
            if (dbContext is DbContext efDbContext)
            {
                efDbContext.ChangeTracker.Clear();
            }

            return false;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            if (dbContext is DbContext efDbContext)
            {
                efDbContext.ChangeTracker.Clear();
            }

            // Email dispatch failure must not surface account existence. The
            // caller still receives a generic success response.
            _logger.LogWarning(
                ex,
                "Customer account claim email dispatch failed for customer_id={CustomerId}",
                customerId);

            return false;
        }
    }

    private async Task SupersedeOtherActiveClaimTokensAsync(
        Guid customerId,
        Guid consumedTokenId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var otherActiveTokens = await dbContext.CustomerAccountClaimTokens
            .Where(token =>
                token.CustomerId == customerId
                && token.Id != consumedTokenId
                && !token.ConsumedAtUtc.HasValue
                && !token.SupersededAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var token in otherActiveTokens)
        {
            token.SupersededAtUtc = utcNow;
        }
    }

    private async Task<int> ConsumeActiveClaimTokenWithNonRelationalProviderAsync(
        Guid claimTokenId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var claimToken = await dbContext.CustomerAccountClaimTokens
            .FirstOrDefaultAsync(token => token.Id == claimTokenId, cancellationToken);

        return claimToken is not null && claimToken.TryConsume(utcNow) ? 1 : 0;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (dbContext is not DbContext efDbContext || !efDbContext.Database.IsRelational())
        {
            return null;
        }

        return await efDbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    private string ResolveLocaleFromRequest()
    {
        var acceptLanguage = ControllerContext.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return _defaultLocale;
        }

        foreach (var entry in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = entry.Split(';', StringSplitOptions.TrimEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            foreach (var supported in SupportedLocales)
            {
                if (candidate.StartsWith(supported, StringComparison.OrdinalIgnoreCase)
                    || supported.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return supported;
                }
            }
        }

        return _defaultLocale;
    }

    private static string GenerateClaimToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
