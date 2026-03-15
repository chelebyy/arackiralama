using Microsoft.Extensions.Logging;
using RentACar.Core.Enums;

namespace RentACar.API.Services;

public sealed class PasswordResetEmailDispatcher(ILogger<PasswordResetEmailDispatcher> logger) : IPasswordResetEmailDispatcher
{
    public Task DispatchAsync(
        AuthPrincipalType principalType,
        string destinationEmail,
        string resetToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Password reset dispatch invoked. principal_type={PrincipalType} expires_at_utc={ExpiresAtUtc}",
            principalType,
            expiresAtUtc);

        return Task.CompletedTask;
    }
}
