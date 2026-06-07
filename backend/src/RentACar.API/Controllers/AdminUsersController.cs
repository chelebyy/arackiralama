using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Auth;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/users")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminUsersController(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IPasswordResetEmailDispatcher emailDispatcher,
    IAuditLogService auditLogService,
    ILogger<AdminUsersController> logger) : BaseApiController
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);
    private const string EntityType = "AdminUser";

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await dbContext.AdminUsers
            .OrderBy(user => user.Email)
            .Select(user => MapToDto(user))
            .ToListAsync(cancellationToken);

        return OkResponse(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminUserCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequestResponse("Email, parola ve ad soyad zorunludur.");
        }

        if (!AuthRoleNames.TryNormalizeAdminRole(request.Role, out var normalizedRole))
        {
            return BadRequestResponse("Rol yalnizca Admin veya SuperAdmin olabilir.");
        }

        var email = request.Email.Trim();
        var normalizedEmail = AdminUser.NormalizeEmail(email);
        var existingUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            return BadRequestResponse("Bu email ile kayitli bir yonetici zaten var.");
        }

        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Role = normalizedRole,
            IsActive = true
        };

        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Create",
            EntityType,
            adminUser.Id.ToString(),
            GetCurrentUserId(),
            null,
            System.Text.Json.JsonSerializer.Serialize(new { request.Email, request.FullName, Role = normalizedRole }),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(MapToDto(adminUser), "Yonetici kullanicisi olusturuldu.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminUserUpdateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequestResponse("Email ve ad soyad zorunludur.");
        }

        if (!AuthRoleNames.TryNormalizeAdminRole(request.Role, out var normalizedRole))
        {
            return BadRequestResponse("Rol yalnizca Admin veya SuperAdmin olabilir.");
        }

        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (adminUser is null)
        {
            return NotFound(ApiResponse<object>.Fail("Yonetici kullanicisi bulunamadi."));
        }

        if (adminUser.Role == AuthRoleNames.SuperAdmin &&
            normalizedRole != AuthRoleNames.SuperAdmin &&
            await IsLastActiveSuperAdminAsync(adminUser.Id, cancellationToken))
        {
            return BadRequestResponse("Son aktif SuperAdmin kullanicisinin rolu dusurulemez.");
        }

        var email = request.Email.Trim();
        var normalizedEmail = AdminUser.NormalizeEmail(email);
        var emailTaken = await dbContext.AdminUsers
            .AnyAsync(user => user.Id != id && user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailTaken)
        {
            return BadRequestResponse("Bu email ile kayitli bir yonetici zaten var.");
        }

        var oldValues = new
        {
            adminUser.Email,
            adminUser.FullName,
            adminUser.Role
        };

        var utcNow = DateTime.UtcNow;
        adminUser.Email = email;
        adminUser.FullName = request.FullName.Trim();
        adminUser.Role = normalizedRole;
        adminUser.TokenVersion += 1;

        var revokedSessionCount = await RevokeActiveAdminSessionsAsync(adminUser.Id, utcNow, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Update",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            System.Text.Json.JsonSerializer.Serialize(oldValues),
            System.Text.Json.JsonSerializer.Serialize(new { adminUser.Email, adminUser.FullName, adminUser.Role }),
            GetClientIpAddress(),
            cancellationToken);

        logger.LogInformation(
            "Admin updated. admin_id={AdminId} token_version={TokenVersion} revoked_session_count={RevokedSessionCount}",
            adminUser.Id,
            adminUser.TokenVersion,
            revokedSessionCount);

        return OkResponse(MapToDto(adminUser), "Yonetici kullanicisi guncellendi.");
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] AdminUserUpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (!AuthRoleNames.TryNormalizeAdminRole(request.Role, out var normalizedRole))
        {
            return BadRequestResponse("Rol yalnizca Admin veya SuperAdmin olabilir.");
        }

        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (adminUser is null)
        {
            return NotFound(ApiResponse<object>.Fail("Yonetici kullanicisi bulunamadi."));
        }

        if (adminUser.Role == AuthRoleNames.SuperAdmin &&
            normalizedRole != AuthRoleNames.SuperAdmin &&
            await IsLastActiveSuperAdminAsync(adminUser.Id, cancellationToken))
        {
            return BadRequestResponse("Son aktif SuperAdmin kullanicisinin rolu dusurulemez.");
        }

        var oldRole = adminUser.Role;

        var utcNow = DateTime.UtcNow;
        adminUser.Role = normalizedRole;
        adminUser.TokenVersion += 1;

        var revokedSessionCount = await RevokeActiveAdminSessionsAsync(adminUser.Id, utcNow, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "UpdateRole",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            System.Text.Json.JsonSerializer.Serialize(new { OldRole = oldRole }),
            System.Text.Json.JsonSerializer.Serialize(new { NewRole = normalizedRole }),
            GetClientIpAddress(),
            cancellationToken);

        logger.LogInformation(
            "Admin role updated. admin_id={AdminId} new_role={Role} token_version={TokenVersion} revoked_session_count={RevokedSessionCount}",
            adminUser.Id,
            adminUser.Role,
            adminUser.TokenVersion,
            revokedSessionCount);

        return OkResponse(MapToDto(adminUser), "Yonetici rolu guncellendi.");
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (adminUser is null)
        {
            return NotFound(ApiResponse<object>.Fail("Yonetici kullanicisi bulunamadi."));
        }

        var wasActive = adminUser.IsActive;
        adminUser.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Activate",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            System.Text.Json.JsonSerializer.Serialize(new { WasActive = wasActive }),
            System.Text.Json.JsonSerializer.Serialize(new { IsActive = true }),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(MapToDto(adminUser), "Yonetici aktif edildi.");
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (adminUser is null)
        {
            return NotFound(ApiResponse<object>.Fail("Yonetici kullanicisi bulunamadi."));
        }

        if (IsCurrentUser(id))
        {
            return BadRequestResponse("Kendi yonetici hesabinizi pasife alamazsiniz.");
        }

        if (adminUser.Role == AuthRoleNames.SuperAdmin &&
            adminUser.IsActive &&
            await IsLastActiveSuperAdminAsync(adminUser.Id, cancellationToken))
        {
            return BadRequestResponse("Son aktif SuperAdmin kullanicisi pasife alinamaz.");
        }

        var wasActive = adminUser.IsActive;

        var utcNow = DateTime.UtcNow;
        adminUser.IsActive = false;
        adminUser.TokenVersion += 1;

        var revokedSessionCount = await RevokeActiveAdminSessionsAsync(adminUser.Id, utcNow, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Deactivate",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            System.Text.Json.JsonSerializer.Serialize(new { WasActive = wasActive }),
            System.Text.Json.JsonSerializer.Serialize(new { IsActive = false }),
            GetClientIpAddress(),
            cancellationToken);

        logger.LogInformation(
            "Admin deactivated. admin_id={AdminId} token_version={TokenVersion} revoked_session_count={RevokedSessionCount}",
            adminUser.Id,
            adminUser.TokenVersion,
            revokedSessionCount);

        return OkResponse(MapToDto(adminUser), "Yonetici pasif edildi.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (adminUser is null)
        {
            return NotFound(ApiResponse<object>.Fail("Yonetici kullanicisi bulunamadi."));
        }

        if (IsCurrentUser(id))
        {
            return BadRequestResponse("Kendi yonetici hesabinizi silemezsiniz.");
        }

        if (adminUser.Role == AuthRoleNames.SuperAdmin &&
            adminUser.IsActive &&
            await IsLastActiveSuperAdminAsync(adminUser.Id, cancellationToken))
        {
            return BadRequestResponse("Son aktif SuperAdmin kullanicisi silinemez.");
        }

        var utcNow = DateTime.UtcNow;
        var oldValues = new
        {
            adminUser.Email,
            adminUser.FullName,
            adminUser.Role,
            adminUser.IsActive
        };

        var revokedSessionCount = await RevokeActiveAdminSessionsAsync(adminUser.Id, utcNow, cancellationToken);
        dbContext.AdminUsers.Remove(adminUser);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "Delete",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            System.Text.Json.JsonSerializer.Serialize(oldValues),
            null,
            GetClientIpAddress(),
            cancellationToken);

        logger.LogInformation(
            "Admin deleted. admin_id={AdminId} revoked_session_count={RevokedSessionCount}",
            id,
            revokedSessionCount);

        return OkResponse(new { success = true }, "Yonetici kullanicisi silindi.");
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> InitiatePasswordReset(Guid id, CancellationToken cancellationToken)
    {
        var adminUser = await dbContext.AdminUsers.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (adminUser is null)
        {
            return NotFound(ApiResponse<object>.Fail("Yonetici kullanicisi bulunamadi."));
        }

        if (!adminUser.IsActive)
        {
            return BadRequestResponse("Sadece aktif yonetici kullanicilari icin sifre sifirlama baslatilabilir.");
        }

        var utcNow = DateTime.UtcNow;
        var resetToken = GenerateResetToken();
        var resetTokenHash = jwtTokenService.HashRefreshToken(resetToken);
        var expiresAtUtc = utcNow.Add(ResetTokenLifetime);

        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            TokenHash = resetTokenHash,
            ExpiresAtUtc = expiresAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            "InitiatePasswordReset",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            null,
            System.Text.Json.JsonSerializer.Serialize(new { ExpiresAtUtc = expiresAtUtc }),
            GetClientIpAddress(),
            cancellationToken);

        try
        {
            await emailDispatcher.DispatchAsync(
                AuthPrincipalType.Admin,
                adminUser.Email,
                resetToken,
                expiresAtUtc,
                "tr-TR",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Admin reset dispatch failed. admin_id={AdminId}",
                adminUser.Id);
        }

        return OkResponse(new { success = true }, "Yonetici sifre sifirlama baglantisi gonderimi baslatildi.");
    }

    private static AdminUserDto MapToDto(AdminUser user) =>
        new(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            IsActive: user.IsActive,
            LastLoginAtUtc: user.LastLoginAtUtc);

    private static string GenerateResetToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(tokenBytes);
    }

    private async Task<int> RevokeActiveAdminSessionsAsync(Guid adminId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var activeSessions = await dbContext.AuthSessions
            .Where(session =>
                session.PrincipalType == AuthPrincipalType.Admin &&
                session.PrincipalId == adminId &&
                !session.RevokedAtUtc.HasValue &&
                session.RefreshTokenExpiresAtUtc > utcNow)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = utcNow;
            session.LastSeenAtUtc = utcNow;
        }

        return activeSessions.Count;
    }

    private async Task<bool> IsLastActiveSuperAdminAsync(Guid adminId, CancellationToken cancellationToken) =>
        !await dbContext.AdminUsers.AnyAsync(
            user =>
                user.Id != adminId &&
                user.Role == AuthRoleNames.SuperAdmin &&
                user.IsActive,
            cancellationToken);

    private bool IsCurrentUser(Guid adminId) =>
        Guid.TryParse(GetCurrentUserId(), out var currentUserId) && currentUserId == adminId;

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
