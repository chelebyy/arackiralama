using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RentACar.API.Authentication;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public class AccessTokenSessionValidatorTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AccessTokenSessionValidatorTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task ValidateAsync_WithActiveAdminSessionAndMatchingTokenVersion_ReturnsNone()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var adminId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.AdminUsers.Add(new AdminUser
        {
            Id = adminId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = "Admin",
            TokenVersion = 3,
            IsActive = true
        });

        dbContext.AuthSessions.Add(CreateSession(AuthPrincipalType.Admin, adminId, sessionId, DateTime.UtcNow.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var validator = CreateValidator(dbContext);
        var principal = CreatePrincipal(AuthPrincipalType.Admin, adminId, sessionId, 3);

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.None);
    }

    [Fact]
    public async Task ValidateAsync_WhenSessionIsRevoked_ReturnsSessionRevoked()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var adminId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.AdminUsers.Add(new AdminUser
        {
            Id = adminId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = "Admin",
            TokenVersion = 1,
            IsActive = true
        });

        dbContext.AuthSessions.Add(CreateSession(
            AuthPrincipalType.Admin,
            adminId,
            sessionId,
            DateTime.UtcNow.AddDays(1),
            revokedAtUtc: DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var validator = CreateValidator(dbContext);
        var principal = CreatePrincipal(AuthPrincipalType.Admin, adminId, sessionId, 1);

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.SessionRevoked);
    }

    [Fact]
    public async Task ValidateAsync_WhenSessionIsReplaced_ReturnsSessionRevoked()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.Customers.Add(new Customer
        {
            Id = customerId,
            Email = "customer@test.com",
            TokenVersion = 4
        });

        dbContext.AuthSessions.Add(CreateSession(
            AuthPrincipalType.Customer,
            customerId,
            sessionId,
            DateTime.UtcNow.AddDays(1),
            replacedBySessionId: Guid.NewGuid()));
        await dbContext.SaveChangesAsync();

        var validator = CreateValidator(dbContext);
        var principal = CreatePrincipal(AuthPrincipalType.Customer, customerId, sessionId, 4);

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.SessionRevoked);
    }

    [Fact]
    public async Task ValidateAsync_WhenSessionIsExpired_ReturnsSessionExpired()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.Customers.Add(new Customer
        {
            Id = customerId,
            Email = "customer@test.com",
            TokenVersion = 4
        });

        dbContext.AuthSessions.Add(CreateSession(AuthPrincipalType.Customer, customerId, sessionId, DateTime.UtcNow.AddMinutes(-1)));
        await dbContext.SaveChangesAsync();

        var validator = CreateValidator(dbContext);
        var principal = CreatePrincipal(AuthPrincipalType.Customer, customerId, sessionId, 4);

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.SessionExpired);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenVersionMismatches_ReturnsTokenVersionMismatch()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.Customers.Add(new Customer
        {
            Id = customerId,
            Email = "customer@test.com",
            TokenVersion = 8
        });

        dbContext.AuthSessions.Add(CreateSession(AuthPrincipalType.Customer, customerId, sessionId, DateTime.UtcNow.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var validator = CreateValidator(dbContext);
        var principal = CreatePrincipal(AuthPrincipalType.Customer, customerId, sessionId, 7);

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.TokenVersionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_WhenClaimsAreMissing_ReturnsMissingRequiredClaims()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var validator = CreateValidator(dbContext);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("principal_type", "Admin")], "Bearer"));

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.MissingRequiredClaims);
    }

    [Fact]
    public async Task ValidateAsync_WhenSessionPrincipalDoesNotMatchToken_ReturnsSessionNotFound()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var adminId = Guid.NewGuid();
        var differentAdminId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.AdminUsers.Add(new AdminUser
        {
            Id = adminId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = "Admin",
            TokenVersion = 1,
            IsActive = true
        });

        dbContext.AuthSessions.Add(CreateSession(AuthPrincipalType.Admin, differentAdminId, sessionId, DateTime.UtcNow.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var validator = CreateValidator(dbContext);
        var principal = CreatePrincipal(AuthPrincipalType.Admin, adminId, sessionId, 1);

        var result = await validator.ValidateAsync(principal);

        result.Should().Be(AccessTokenSessionValidationFailure.SessionNotFound);
    }

    private static AccessTokenSessionValidator CreateValidator(RentACarDbContext dbContext) =>
        new(dbContext, NullLogger<AccessTokenSessionValidator>.Instance);

    private static ClaimsPrincipal CreatePrincipal(
        AuthPrincipalType principalType,
        Guid principalId,
        Guid sessionId,
        int tokenVersion)
    {
        var claims = new[]
        {
            new Claim("principal_type", principalType.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, principalId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sid, sessionId.ToString()),
            new Claim("ver", tokenVersion.ToString(CultureInfo.InvariantCulture))
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static AuthSession CreateSession(
        AuthPrincipalType principalType,
        Guid principalId,
        Guid sessionId,
        DateTime refreshTokenExpiresAtUtc,
        DateTime? revokedAtUtc = null,
        Guid? replacedBySessionId = null)
    {
        return new AuthSession
        {
            Id = sessionId,
            PrincipalType = principalType,
            PrincipalId = principalId,
            RefreshTokenHash = $"sha256:{Guid.NewGuid():N}",
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            RevokedAtUtc = revokedAtUtc,
            ReplacedBySessionId = replacedBySessionId,
            LastSeenAtUtc = DateTime.UtcNow
        };
    }
}
