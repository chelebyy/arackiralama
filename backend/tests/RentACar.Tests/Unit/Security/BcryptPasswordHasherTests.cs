using FluentAssertions;
using RentACar.Infrastructure.Security;
using Xunit;

namespace RentACar.Tests.Unit.Security;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsBCryptHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().StartWith("$2"); // BCrypt format
        hash.Should().NotBe(password);
    }

    [Fact]
    public void HashPassword_WithNullPassword_ThrowsArgumentException()
    {
        // Act
        var act = () => _hasher.HashPassword(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("password");
    }

    [Fact]
    public void HashPassword_WithEmptyPassword_ThrowsArgumentException()
    {
        // Act
        var act = () => _hasher.HashPassword(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("password");
    }

    [Fact]
    public void HashPassword_WithWhitespacePassword_ThrowsArgumentException()
    {
        // Act
        var act = () => _hasher.HashPassword("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("password");
    }

    [Fact]
    public void HashPassword_GeneratesUniqueHashesForSamePassword()
    {
        // Arrange
        var password = "SamePassword123!";

        // Act
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2); // BCrypt generates unique salts
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "CorrectPassword123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "CorrectPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithNullPassword_ReturnsFalse()
    {
        // Arrange
        var hash = _hasher.HashPassword("SomePassword123!");

        // Act
        var result = _hasher.VerifyPassword(null!, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithNullHash_ReturnsFalse()
    {
        // Arrange
        var password = "SomePassword123!";

        // Act
        var result = _hasher.VerifyPassword(password, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var hash = _hasher.HashPassword("SomePassword123!");

        // Act
        var result = _hasher.VerifyPassword(string.Empty, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithEmptyHash_ReturnsFalse()
    {
        // Arrange
        var password = "SomePassword123!";

        // Act
        var result = _hasher.VerifyPassword(password, string.Empty);

        // Assert
        result.Should().BeFalse();
    }
}
