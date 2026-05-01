using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.API.Services;

public sealed class PasswordResetEmailDispatcher(
    INotificationQueueService notificationQueueService,
    ILogger<PasswordResetEmailDispatcher> logger) : IPasswordResetEmailDispatcher
{
    private readonly INotificationQueueService _notificationQueueService = notificationQueueService;

    public Task DispatchAsync(
        AuthPrincipalType principalType,
        string destinationEmail,
        string resetToken,
        DateTime expiresAtUtc,
        string locale = "tr-TR",
        CancellationToken cancellationToken = default)
    {
        return DispatchInternalAsync(principalType, destinationEmail, resetToken, expiresAtUtc, locale, cancellationToken);
    }

    private async Task DispatchInternalAsync(
        AuthPrincipalType principalType,
        string destinationEmail,
        string resetToken,
        DateTime expiresAtUtc,
        string locale,
        CancellationToken cancellationToken)
    {
        var templateKey = principalType == AuthPrincipalType.Admin
            ? NotificationTemplateKeys.PasswordResetAdmin
            : NotificationTemplateKeys.PasswordResetCustomer;

        await _notificationQueueService.EnqueueEmailAsync(
            new QueuedEmailNotificationRequest
            {
                ToEmail = destinationEmail,
                TemplateKey = templateKey,
                Locale = locale,
                Variables = new Dictionary<string, string>
                {
                    ["Token"] = resetToken,
                    ["ExpiresAtUtc"] = expiresAtUtc.ToString("u")
                }
            },
            cancellationToken: cancellationToken);

        logger.LogInformation("Password reset email queued.");
    }
}
