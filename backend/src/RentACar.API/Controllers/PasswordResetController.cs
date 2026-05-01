using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Auth;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/v1/auth/password-reset")]
public sealed class PasswordResetController(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IPasswordResetEmailDispatcher emailDispatcher,
    ILogger<PasswordResetController> logger) : BaseApiController
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    [HttpPost("request")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> RequestReset([FromBody] PasswordResetRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.PrincipalScope) ||
            !TryParsePrincipalScope(request.PrincipalScope, out var principalType))
        {
            return BadRequestResponse("Email ve principal kapsamı zorunludur.");
        }

        var utcNow = DateTime.UtcNow;
        var email = request.Email.Trim();

        if (principalType == AuthPrincipalType.Admin)
        {
            var normalizedEmail = AdminUser.NormalizeEmail(email);
            var admin = await dbContext.AdminUsers
                .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

            if (admin is not null && admin.IsActive)
            {
                await CreateAndDispatchResetTokenAsync(
                    principalType,
                    admin.Id,
                    admin.Email,
                    utcNow,
                    cancellationToken);
            }
        }
        else
        {
            var normalizedEmail = Customer.NormalizeEmail(email);
            var customer = await dbContext.Customers
                .FirstOrDefaultAsync(existingCustomer => existingCustomer.NormalizedEmail == normalizedEmail, cancellationToken);

            if (customer is not null && customer.HasPassword)
            {
                await CreateAndDispatchResetTokenAsync(
                    principalType,
                    customer.Id,
                    customer.Email,
                    utcNow,
                    cancellationToken);
            }
        }

        return OkResponse(new { success = true }, "Parola sıfırlama isteği alındı.");
    }

    [HttpPost("confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Confirm([FromBody] PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.PrincipalScope) ||
            !TryParsePrincipalScope(request.PrincipalScope, out var principalType))
        {
            return BadRequestResponse("Token, yeni parola ve principal kapsamı zorunludur.");
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = jwtTokenService.HashRefreshToken(request.Token);

        var resetToken = await dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(token =>
                token.PrincipalType == principalType &&
                token.TokenHash == tokenHash,
                cancellationToken);

        if (resetToken is null || !resetToken.IsActive(utcNow) || !resetToken.TryConsume(utcNow))
        {
            return BadRequestResponse("Geçersiz veya süresi dolmuş parola sıfırlama bağlantısı.");
        }

        if (principalType == AuthPrincipalType.Admin)
        {
            var admin = await dbContext.AdminUsers
                .FirstOrDefaultAsync(user => user.Id == resetToken.PrincipalId, cancellationToken);

            if (admin is null || !admin.IsActive)
            {
                return BadRequestResponse("Geçersiz veya süresi dolmuş parola sıfırlama bağlantısı.");
            }

            admin.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            admin.TokenVersion += 1;
        }
        else
        {
            var customer = await dbContext.Customers
                .FirstOrDefaultAsync(existingCustomer => existingCustomer.Id == resetToken.PrincipalId, cancellationToken);

            if (customer is null || !customer.HasPassword)
            {
                return BadRequestResponse("Geçersiz veya süresi dolmuş parola sıfırlama bağlantısı.");
            }

            customer.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            customer.TokenVersion += 1;
        }

        var activeSessions = await dbContext.AuthSessions
            .Where(session =>
                session.PrincipalType == principalType &&
                session.PrincipalId == resetToken.PrincipalId &&
                !session.RevokedAtUtc.HasValue &&
                session.RefreshTokenExpiresAtUtc > utcNow)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = utcNow;
            session.LastSeenAtUtc = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Password reset confirmed. principal_type={PrincipalType} principal_id={PrincipalId} revoked_session_count={RevokedSessionCount}",
            principalType,
            resetToken.PrincipalId,
            activeSessions.Count);

        return OkResponse(new { success = true }, "Parola başarıyla güncellendi.");
    }

    private async Task CreateAndDispatchResetTokenAsync(
        AuthPrincipalType principalType,
        Guid principalId,
        string destinationEmail,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var resetToken = GenerateResetToken();
        var resetTokenHash = jwtTokenService.HashRefreshToken(resetToken);
        var expiresAtUtc = utcNow.Add(ResetTokenLifetime);

        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PrincipalType = principalType,
            PrincipalId = principalId,
            TokenHash = resetTokenHash,
            ExpiresAtUtc = expiresAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await emailDispatcher.DispatchAsync(
                principalType,
                destinationEmail,
                resetToken,
                expiresAtUtc,
                "tr-TR",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Password reset dispatch failed. principal_type={PrincipalType} principal_id={PrincipalId}",
                principalType,
                principalId);
        }
    }

    private static bool TryParsePrincipalScope(string scope, out AuthPrincipalType principalType)
    {
        if (scope.Equals(AuthPrincipalType.Customer.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            principalType = AuthPrincipalType.Customer;
            return true;
        }

        if (scope.Equals(AuthPrincipalType.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            principalType = AuthPrincipalType.Admin;
            return true;
        }

        principalType = default;
        return false;
    }

    private static string GenerateResetToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(tokenBytes);
    }
}
