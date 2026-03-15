using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Integration.Data;

public class DbContextTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _factory;

    public DbContextTests(TestDbContextFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CanCreateInMemoryDatabase()
    {
        // Act
        using var context = _factory.CreateContext();

        // Assert
        context.Should().NotBeNull();
        context.Database.Should().NotBeNull();
    }

    [Fact]
    public async Task CanConnectToDatabase()
    {
        // Arrange
        using var context = _factory.CreateContext();

        // Act
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
    }

    [Fact]
    public void CustomerAndAdminEntities_ShouldMapNormalizedAuthStateColumns()
    {
        // Arrange
        using var context = _factory.CreateContext();

        // Act
        var customerEntity = context.Model.FindEntityType(typeof(Customer));
        var adminEntity = context.Model.FindEntityType(typeof(AdminUser));

        // Assert
        customerEntity.Should().NotBeNull();
        customerEntity!.GetTableName().Should().Be("customers");
        customerEntity.FindProperty(nameof(Customer.NormalizedEmail))!.GetColumnName().Should().Be("normalized_email");
        customerEntity.FindProperty(nameof(Customer.FailedLoginCount))!.GetColumnName().Should().Be("failed_login_count");
        customerEntity.FindProperty(nameof(Customer.LockoutEndUtc))!.GetColumnName().Should().Be("lockout_end_utc");
        customerEntity.FindProperty(nameof(Customer.LastLoginAtUtc))!.GetColumnName().Should().Be("last_login_at_utc");
        customerEntity.FindProperty(nameof(Customer.TokenVersion))!.GetColumnName().Should().Be("token_version");
        customerEntity.GetIndexes().Should().Contain(x =>
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Customer.NormalizedEmail) }) &&
            x.IsUnique);

        adminEntity.Should().NotBeNull();
        adminEntity!.GetTableName().Should().Be("admin_users");
        adminEntity.FindProperty(nameof(AdminUser.NormalizedEmail))!.GetColumnName().Should().Be("normalized_email");
        adminEntity.FindProperty(nameof(AdminUser.FailedLoginCount))!.GetColumnName().Should().Be("failed_login_count");
        adminEntity.FindProperty(nameof(AdminUser.LockoutEndUtc))!.GetColumnName().Should().Be("lockout_end_utc");
        adminEntity.FindProperty(nameof(AdminUser.LastLoginAtUtc))!.GetColumnName().Should().Be("last_login_at_utc");
        adminEntity.FindProperty(nameof(AdminUser.TokenVersion))!.GetColumnName().Should().Be("token_version");
        adminEntity.GetIndexes().Should().Contain(x =>
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(AdminUser.NormalizedEmail) }) &&
            x.IsUnique);
    }

    [Fact]
    public void AuthSessionAndPasswordResetTokenEntities_ShouldBeRegisteredWithSnakeCaseAndIndexes()
    {
        // Arrange
        using var context = _factory.CreateContext();

        // Act
        var sessionEntity = context.Model.FindEntityType(typeof(AuthSession));
        var resetTokenEntity = context.Model.FindEntityType(typeof(PasswordResetToken));

        // Assert
        sessionEntity.Should().NotBeNull();
        sessionEntity!.GetTableName().Should().Be("auth_sessions");
        sessionEntity.FindProperty(nameof(AuthSession.PrincipalType))!.GetColumnName().Should().Be("principal_type");
        sessionEntity.FindProperty(nameof(AuthSession.PrincipalId))!.GetColumnName().Should().Be("principal_id");
        sessionEntity.FindProperty(nameof(AuthSession.RefreshTokenHash))!.GetColumnName().Should().Be("refresh_token_hash");
        sessionEntity.FindProperty(nameof(AuthSession.RefreshTokenExpiresAtUtc))!.GetColumnName().Should().Be("refresh_token_expires_at_utc");
        sessionEntity.GetIndexes().Should().Contain(x =>
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(AuthSession.PrincipalType), nameof(AuthSession.PrincipalId) }));
        sessionEntity.GetIndexes().Should().Contain(x =>
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(AuthSession.RefreshTokenHash) }) &&
            x.IsUnique);

        resetTokenEntity.Should().NotBeNull();
        resetTokenEntity!.GetTableName().Should().Be("password_reset_tokens");
        resetTokenEntity.FindProperty(nameof(PasswordResetToken.PrincipalType))!.GetColumnName().Should().Be("principal_type");
        resetTokenEntity.FindProperty(nameof(PasswordResetToken.PrincipalId))!.GetColumnName().Should().Be("principal_id");
        resetTokenEntity.FindProperty(nameof(PasswordResetToken.TokenHash))!.GetColumnName().Should().Be("token_hash");
        resetTokenEntity.FindProperty(nameof(PasswordResetToken.ExpiresAtUtc))!.GetColumnName().Should().Be("expires_at_utc");
        resetTokenEntity.GetIndexes().Should().Contain(x =>
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PasswordResetToken.PrincipalType), nameof(PasswordResetToken.PrincipalId) }));
        resetTokenEntity.GetIndexes().Should().Contain(x =>
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PasswordResetToken.TokenHash) }) &&
            x.IsUnique);
    }
}
