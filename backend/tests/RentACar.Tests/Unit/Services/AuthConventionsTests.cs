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
    public void IsAdminRole_WithNonAdminRoles_ReturnsFalse(string role)
    {
        AuthRoleNames.IsAdminRole(role).Should().BeFalse();
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
