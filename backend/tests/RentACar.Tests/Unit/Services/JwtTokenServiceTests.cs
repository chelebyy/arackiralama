using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Options;
using RentACar.API.Services;
using RentACar.Core.Entities;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private const string ValidSecret = "ThisIsAVeryLongSecretKeyForTesting123456789!";

    public JwtTokenServiceTests()
    {
        _service = CreateService();
    }

    [Fact]
    public void CreateAdminAccessToken_WithValidUser_ReturnsValidToken()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();

        // Act
        var token = _service.CreateAdminAccessToken(adminUser, Guid.NewGuid(), out var expiresAtUtc);

        // Assert
        token.Should().NotBeNullOrEmpty();
        expiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void CreateAdminAccessToken_WithValidUser_ContainsCorrectClaims()
    {
        // Arrange
        var adminUser = CreateTestAdminUser(tokenVersion: 7);
        var sessionId = Guid.NewGuid();

        // Act
        var token = _service.CreateAdminAccessToken(adminUser, sessionId, out _);
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == adminUser.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == adminUser.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sid && c.Value == sessionId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == AuthClaimTypes.TokenVersion && c.Value == "7");
        jwtToken.Claims.Should().Contain(c => c.Type == AuthClaimTypes.PrincipalType && c.Value == "Admin");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == adminUser.Role);
        jwtToken.Claims.Should().Contain(c => c.Type == AuthClaimTypes.Role && c.Value == adminUser.Role);
        jwtToken.Claims.Should().Contain(c => c.Type == AuthClaimTypes.Permission && c.Value == AuthPermissionNames.AdminAccess);
    }

    [Fact]
    public void CreateCustomerAccessToken_WithValidCustomer_ContainsCorrectClaims()
    {
        // Arrange
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Email = "customer@test.com",
            TokenVersion = 3
        };
        var sessionId = Guid.NewGuid();

        // Act
        var token = _service.CreateCustomerAccessToken(customer, sessionId, out _);
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == customer.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == customer.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sid && c.Value == sessionId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == AuthClaimTypes.TokenVersion && c.Value == "3");
        jwtToken.Claims.Should().Contain(c => c.Type == AuthClaimTypes.PrincipalType && c.Value == "Customer");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == AuthRoleNames.Customer);
        jwtToken.Claims.Should().NotContain(c => c.Type == AuthClaimTypes.Permission);
    }

    [Fact]
    public void CreateAdminAccessToken_WithNullRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = null!
        };

        // Act
        var act = () => _service.CreateAdminAccessToken(adminUser, Guid.NewGuid(), out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*role is required*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithEmptyRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = string.Empty
        };

        // Act
        var act = () => _service.CreateAdminAccessToken(adminUser, Guid.NewGuid(), out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*role is required*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithUnsupportedRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = "Operations"
        };

        // Act
        var act = () => _service.CreateAdminAccessToken(adminUser, Guid.NewGuid(), out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be either Admin or SuperAdmin*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithEmptySessionId_ThrowsInvalidOperationException()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();

        // Act
        var act = () => _service.CreateAdminAccessToken(adminUser, Guid.Empty, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Session id is required*");
    }

    [Fact]
    public void CreateCustomerAccessToken_WithEmptySessionId_ThrowsInvalidOperationException()
    {
        // Arrange
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Email = "customer@test.com"
        };

        // Act
        var act = () => _service.CreateCustomerAccessToken(customer, Guid.Empty, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Session id is required*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithShortSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var serviceWithShortSecret = CreateService(secret: "TooShort");
        var adminUser = CreateTestAdminUser();

        // Act
        var act = () => serviceWithShortSecret.CreateAdminAccessToken(adminUser, Guid.NewGuid(), out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*at least 32 characters*");
    }

    [Fact]
    public void CreateAdminAccessToken_Sets15MinuteExpiryTime()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();
        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);

        // Act
        _service.CreateAdminAccessToken(adminUser, Guid.NewGuid(), out var expiresAtUtc);

        // Assert
        expiresAtUtc.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void CreateRefreshToken_WithDefaultOptions_ReturnsUniqueOpaqueTokenAnd7DayExpiry()
    {
        // Arrange
        var expectedExpiry = DateTime.UtcNow.AddDays(7);

        // Act
        var tokenA = _service.CreateRefreshToken(out var expiresAtUtc);
        var tokenB = _service.CreateRefreshToken(out _);

        // Assert
        tokenA.Should().NotBeNullOrWhiteSpace();
        tokenB.Should().NotBeNullOrWhiteSpace();
        tokenA.Should().NotBe(tokenB);
        tokenA.Length.Should().BeGreaterThan(70);
        expiresAtUtc.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void HashRefreshToken_WithValidToken_ReturnsSha256PrefixedHash()
    {
        // Arrange
        var refreshToken = _service.CreateRefreshToken(out _);

        // Act
        var hash = _service.HashRefreshToken(refreshToken);

        // Assert
        hash.Should().StartWith("sha256:");
        hash.Should().HaveLength("sha256:".Length + 64);
    }

    [Fact]
    public void VerifyRefreshToken_WithMatchingTokenAndHash_ReturnsTrue()
    {
        // Arrange
        var refreshToken = _service.CreateRefreshToken(out _);
        var hash = _service.HashRefreshToken(refreshToken);

        // Act
        var isValid = _service.VerifyRefreshToken(refreshToken, hash);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsRefreshTokenReplay_WhenUsingRotatedOutToken_ReturnsTrue()
    {
        // Arrange
        var oldToken = _service.CreateRefreshToken(out _);
        var newToken = _service.CreateRefreshToken(out _);
        var activeHash = _service.HashRefreshToken(newToken);

        // Act
        var replayDetected = _service.IsRefreshTokenReplay(oldToken, activeHash);

        // Assert
        replayDetected.Should().BeTrue();
    }

    [Fact]
    public void IsRefreshTokenReplay_WhenUsingActiveToken_ReturnsFalse()
    {
        // Arrange
        var activeToken = _service.CreateRefreshToken(out _);
        var activeHash = _service.HashRefreshToken(activeToken);

        // Act
        var replayDetected = _service.IsRefreshTokenReplay(activeToken, activeHash);

        // Assert
        replayDetected.Should().BeFalse();
    }

    [Fact]
    public void HashRefreshToken_WithEmptyToken_ThrowsArgumentException()
    {
        // Act
        var act = () => _service.HashRefreshToken(" ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Refresh token cannot be null or empty*");
    }

    private static JwtTokenService CreateService(
        string? secret = ValidSecret,
        int accessTokenMinutes = 15,
        int refreshTokenDays = 7)
    {
        var options = new JwtOptions
        {
            Secret = secret ?? string.Empty,
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenMinutes = accessTokenMinutes,
            RefreshTokenDays = refreshTokenDays
        };

        return new JwtTokenService(Options.Create(options));
    }

    private static AdminUser CreateTestAdminUser(int tokenVersion = 0)
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            Role = "Admin",
            TokenVersion = tokenVersion
        };
    }
}
