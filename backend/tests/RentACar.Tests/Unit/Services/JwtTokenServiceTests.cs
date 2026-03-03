using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = ValidSecret,
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:AccessTokenHours"] = "1"
            })
            .Build();

        _service = new JwtTokenService(config);
    }

    [Fact]
    public void CreateAdminAccessToken_WithValidUser_ReturnsValidToken()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();

        // Act
        var token = _service.CreateAdminAccessToken(adminUser, out var expiresAtUtc);

        // Assert
        token.Should().NotBeNullOrEmpty();
        expiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void CreateAdminAccessToken_WithValidUser_ContainsCorrectClaims()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();

        // Act
        var token = _service.CreateAdminAccessToken(adminUser, out _);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == adminUser.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == adminUser.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == adminUser.Role);
        jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == adminUser.Role);
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
        var act = () => _service.CreateAdminAccessToken(adminUser, out _);

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
        var act = () => _service.CreateAdminAccessToken(adminUser, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*role is required*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithWhitespaceRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var adminUser = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = "   "
        };

        // Act
        var act = () => _service.CreateAdminAccessToken(adminUser, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*role is required*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithShortSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var shortSecretConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "TooShort",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        var serviceWithShortSecret = new JwtTokenService(shortSecretConfig);
        var adminUser = CreateTestAdminUser();

        // Act
        var act = () => serviceWithShortSecret.CreateAdminAccessToken(adminUser, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*at least 32 characters*");
    }

    [Fact]
    public void CreateAdminAccessToken_WithNullSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var nullSecretConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        var serviceWithNullSecret = new JwtTokenService(nullSecretConfig);
        var adminUser = CreateTestAdminUser();

        // Act
        var act = () => serviceWithNullSecret.CreateAdminAccessToken(adminUser, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*at least 32 characters*");
    }

    [Fact]
    public void CreateAdminAccessToken_SetsCorrectExpiryTime()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();
        var expectedExpiry = DateTime.UtcNow.AddHours(1);

        // Act
        _service.CreateAdminAccessToken(adminUser, out var expiresAtUtc);

        // Assert
        expiresAtUtc.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void CreateAdminAccessToken_ContainsPermissionClaim()
    {
        // Arrange
        var adminUser = CreateTestAdminUser();

        // Act
        var token = _service.CreateAdminAccessToken(adminUser, out _);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        jwtToken.Claims.Should().Contain(c => c.Type == "Permission" && c.Value == "admin.access");
    }

    private static AdminUser CreateTestAdminUser()
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            Role = "Admin"
        };
    }
}
