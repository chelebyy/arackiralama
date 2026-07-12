using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;

namespace RentACar.API.Services;

public sealed class CustomerAccountClaimEmailDispatcher(
    INotificationQueueService notificationQueueService,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<CustomerAccountClaimEmailDispatcher> logger) : ICustomerAccountClaimEmailDispatcher
{
    private readonly INotificationQueueService _notificationQueueService = notificationQueueService;
    private readonly string _defaultLocale = string.IsNullOrWhiteSpace(notificationOptions.Value.DefaultLocale)
        ? "tr-TR"
        : notificationOptions.Value.DefaultLocale;

    public async Task DispatchAsync(
        string destinationEmail,
        string rawToken,
        DateTime expiresAtUtc,
        string locale,
        CancellationToken cancellationToken = default)
    {
        var resolvedLocale = string.IsNullOrWhiteSpace(locale) ? _defaultLocale : locale;
        var claimUrl = BuildClaimUrl(resolvedLocale, rawToken);

        await _notificationQueueService.EnqueueEmailAsync(
            new QueuedEmailNotificationRequest
            {
                ToEmail = destinationEmail,
                TemplateKey = NotificationTemplateKeys.CustomerAccountClaim,
                Locale = resolvedLocale,
                Variables = new Dictionary<string, string>
                {
                    ["ClaimUrl"] = claimUrl,
                    ["ExpiresAtUtc"] = expiresAtUtc.ToString("u")
                }
            },
            cancellationToken: cancellationToken);

        logger.LogInformation("Customer account claim email queued.");
    }

    private static string BuildClaimUrl(string locale, string rawToken)
    {
        // Locale values are app-controlled (tr-TR/en-US/ru-RU/ar-SA/de-DE) and the
        // token is opaque base64url, so embedding it in the URL is safe.
        var localeSegment = locale.Trim().Split('-', 2)[0].ToLowerInvariant();
        return $"/{localeSegment}/account-claim?token={Uri.EscapeDataString(rawToken)}";
    }
}
