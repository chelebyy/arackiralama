using FluentAssertions;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using Xunit;

namespace RentACar.Tests.Unit.Entities;

public class AuthPersistenceModelsTests
{
    [Fact]
    public void AuthSession_ShouldStoreRefreshTokenHash_AndExposeActiveState()
    {
        var now = DateTime.UtcNow;
        var session = new AuthSession
        {
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = Guid.NewGuid(),
            RefreshTokenHash = "sha256:abcdef123456",
            RefreshTokenExpiresAtUtc = now.AddMinutes(30)
        };

        session.RefreshTokenHash.Should().Be("sha256:abcdef123456");
        session.IsActive(now).Should().BeTrue();
        session.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void AuthSession_IsActive_ShouldReturnFalse_WhenExpiredOrRevoked()
    {
        var now = DateTime.UtcNow;
        var expired = new AuthSession
        {
            RefreshTokenHash = "sha256:expired",
            RefreshTokenExpiresAtUtc = now.AddSeconds(-1)
        };

        var revoked = new AuthSession
        {
            RefreshTokenHash = "sha256:revoked",
            RefreshTokenExpiresAtUtc = now.AddMinutes(10),
            RevokedAtUtc = now
        };

        expired.IsActive(now).Should().BeFalse();
        revoked.IsActive(now).Should().BeFalse();
        revoked.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void PasswordResetToken_TryConsume_ShouldAllowSingleUse()
    {
        var now = DateTime.UtcNow;
        var resetToken = new PasswordResetToken
        {
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = Guid.NewGuid(),
            TokenHash = "sha256:reset",
            ExpiresAtUtc = now.AddMinutes(30)
        };

        var firstConsume = resetToken.TryConsume(now.AddMinutes(1));
        var secondConsume = resetToken.TryConsume(now.AddMinutes(2));

        firstConsume.Should().BeTrue();
        secondConsume.Should().BeFalse();
        resetToken.IsConsumed.Should().BeTrue();
        resetToken.ConsumedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void PasswordResetToken_TryConsume_ShouldFail_WhenExpired()
    {
        var now = DateTime.UtcNow;
        var resetToken = new PasswordResetToken
        {
            TokenHash = "sha256:expired-reset",
            ExpiresAtUtc = now.AddMinutes(-1)
        };

        resetToken.IsActive(now).Should().BeFalse();
        resetToken.TryConsume(now).Should().BeFalse();
        resetToken.ConsumedAtUtc.Should().BeNull();
    }

    [Fact]
    public void AuthEntities_ShouldNotExposePlaintextTokenProperties()
    {
        typeof(AuthSession).GetProperty("RefreshToken").Should().BeNull();
        typeof(PasswordResetToken).GetProperty("Token").Should().BeNull();
    }
}
