using Microsoft.Extensions.Options;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;

namespace RentACar.API.Services;

public sealed class PasswordResetEmailDispatcher(
    INotificationQueueService notificationQueueService,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<PasswordResetEmailDispatcher> logger) : IPasswordResetEmailDispatcher
{
    private readonly INotificationQueueService _notificationQueueService = notificationQueueService;
    private readonly string _defaultLocale = string.IsNullOrWhiteSpace(notificationOptions.Value.DefaultLocale)
        ? "tr-TR"
        : notificationOptions.Value.DefaultLocale;

    public Task DispatchAsync(
        AuthPrincipalType principalType,
        string destinationEmail,
        string resetToken,
        DateTime expiresAtUtc,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchInternalAsync(principalType, destinationEmail, resetToken, expiresAtUtc, locale, cancellationToken);
    }

    private async Task DispatchInternalAsync(
        AuthPrincipalType principalType,
        string destinationEmail,
        string resetToken,
        DateTime expiresAtUtc,
        string? locale,
        CancellationToken cancellationToken)
    {
        var templateKey = principalType == AuthPrincipalType.Admin
            ? NotificationTemplateKeys.PasswordResetAdmin
            : NotificationTemplateKeys.PasswordResetCustomer;
        var resolvedLocale = string.IsNullOrWhiteSpace(locale)
            ? _defaultLocale
            : locale;

        await _notificationQueueService.EnqueueEmailAsync(
            new QueuedEmailNotificationRequest
            {
                ToEmail = destinationEmail,
                TemplateKey = templateKey,
                Locale = resolvedLocale,
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
