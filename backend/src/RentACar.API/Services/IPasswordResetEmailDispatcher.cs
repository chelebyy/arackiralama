using RentACar.Core.Enums;

namespace RentACar.API.Services;

public interface IPasswordResetEmailDispatcher
{
    Task DispatchAsync(
        AuthPrincipalType principalType,
        string destinationEmail,
        string resetToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken);
}
