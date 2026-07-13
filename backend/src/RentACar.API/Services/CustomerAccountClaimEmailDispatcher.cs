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
    private readonly string _publicFrontendBaseUrl = notificationOptions.Value.PublicFrontendBaseUrl;

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

    private string BuildClaimUrl(string locale, string rawToken)
    {
        // Locale values are app-controlled (tr-TR/en-US/ru-RU/ar-SA/de-DE) and the
        // token is opaque base64url, so embedding it in the URL is safe.
        var localeSegment = locale.Trim().Split('-', 2)[0].ToLowerInvariant();
        var publicFrontendBaseUri = CreatePublicFrontendBaseUri(_publicFrontendBaseUrl);
        var claimUri = new Uri(publicFrontendBaseUri, $"{localeSegment}/account-claim");
        return $"{claimUri.AbsoluteUri}?token={Uri.EscapeDataString(rawToken)}";
    }

    private static Uri CreatePublicFrontendBaseUri(string configuredBaseUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var publicFrontendBaseUri)
            || (publicFrontendBaseUri.Scheme != Uri.UriSchemeHttp && publicFrontendBaseUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(publicFrontendBaseUri.Query)
            || !string.IsNullOrEmpty(publicFrontendBaseUri.Fragment))
        {
            throw new InvalidOperationException(
                "Notifications:PublicFrontendBaseUrl must be an absolute HTTP or HTTPS URL without a query or fragment.");
        }

        return new Uri($"{publicFrontendBaseUri.AbsoluteUri.TrimEnd('/')}/", UriKind.Absolute);
    }
}
