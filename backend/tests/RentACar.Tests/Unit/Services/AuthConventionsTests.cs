using FluentAssertions;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public class AuthConventionsTests
{
    [Theory]
    [InlineData(AuthRoleNames.Admin)]
    [InlineData(AuthRoleNames.SuperAdmin)]
    public void IsAdminRole_WithAdminScopeRoles_ReturnsTrue(string role)
    {
        AuthRoleNames.IsAdminRole(role).Should().BeTrue();
    }

    [Theory]
    [InlineData(AuthRoleNames.Customer)]
    [InlineData(AuthRoleNames.Guest)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public void IsAdminRole_WithNonAdminRoles_ReturnsFalse(string role)
    {
        AuthRoleNames.IsAdminRole(role).Should().BeFalse();
    }

    [Theory]
    [InlineData(AuthRoleNames.Admin, AuthRoleNames.Admin)]
    [InlineData(AuthRoleNames.SuperAdmin, AuthRoleNames.SuperAdmin)]
    [InlineData(" Admin ", AuthRoleNames.Admin)]
    [InlineData(" SuperAdmin ", AuthRoleNames.SuperAdmin)]
    public void TryNormalizeAdminRole_WithSupportedRoles_ReturnsCanonicalValue(string role, string expectedCanonicalRole)
    {
        var isSupported = AuthRoleNames.TryNormalizeAdminRole(role, out var normalizedRole);

        isSupported.Should().BeTrue();
        normalizedRole.Should().Be(expectedCanonicalRole);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    [InlineData("Manager")]
    public void TryNormalizeAdminRole_WithInvalidOrCasingDriftRoles_ReturnsFalse(string role)
    {
        var isSupported = AuthRoleNames.TryNormalizeAdminRole(role, out var normalizedRole);

        isSupported.Should().BeFalse();
        normalizedRole.Should().BeEmpty();
    }

    [Fact]
    public void AuthPolicyNames_ExposeGuestCustomerAdminAndSuperAdminPolicies()
    {
        AuthPolicyNames.GuestOnly.Should().Be("GuestOnly");
        AuthPolicyNames.CustomerOnly.Should().Be("CustomerOnly");
        AuthPolicyNames.AdminOnly.Should().Be("AdminOnly");
        AuthPolicyNames.SuperAdminOnly.Should().Be("SuperAdminOnly");
    }
}
