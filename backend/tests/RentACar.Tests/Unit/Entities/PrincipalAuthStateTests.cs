using FluentAssertions;
using RentACar.Core.Entities;
using Xunit;

namespace RentACar.Tests.Unit.Entities;

public class PrincipalAuthStateTests
{
    [Fact]
    public void Customer_EmailSetter_ShouldNormalizeEmailAndTrimSource()
    {
        // Arrange
        var customer = new Customer();

        // Act
        customer.Email = "  User.Name+tag@example.com  ";

        // Assert
        customer.Email.Should().Be("User.Name+tag@example.com");
        customer.NormalizedEmail.Should().Be("USER.NAME+TAG@EXAMPLE.COM");
    }

    [Fact]
    public void AdminUser_EmailSetter_ShouldNormalizeEmailAndTrimSource()
    {
        // Arrange
        var admin = new AdminUser();

        // Act
        admin.Email = "  Admin@Test.com  ";

        // Assert
        admin.Email.Should().Be("Admin@Test.com");
        admin.NormalizedEmail.Should().Be("ADMIN@TEST.COM");
    }

    [Fact]
    public void PrincipalAuthFields_ShouldHaveSecureDefaults()
    {
        // Arrange
        var customer = new Customer();
        var admin = new AdminUser();

        // Assert
        customer.PasswordHash.Should().BeNull();
        customer.HasPassword.Should().BeFalse();
        customer.FailedLoginCount.Should().Be(0);
        customer.LockoutEndUtc.Should().BeNull();
        customer.LastLoginAtUtc.Should().BeNull();
        customer.TokenVersion.Should().Be(0);

        admin.FailedLoginCount.Should().Be(0);
        admin.LockoutEndUtc.Should().BeNull();
        admin.LastLoginAtUtc.Should().BeNull();
        admin.TokenVersion.Should().Be(0);
    }

    [Fact]
    public void Customer_HasPassword_ShouldTrackCredentialPresence()
    {
        // Arrange
        var customer = new Customer
        {
            PasswordHash = "   "
        };

        // Act
        var hasPasswordWithWhitespace = customer.HasPassword;
        customer.PasswordHash = "bcrypt-hash";

        // Assert
        hasPasswordWithWhitespace.Should().BeFalse();
        customer.HasPassword.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeEmail_ShouldReturnEmpty_ForBlankInput(string input)
    {
        Customer.NormalizeEmail(input).Should().BeEmpty();
        AdminUser.NormalizeEmail(input).Should().BeEmpty();
    }
}
