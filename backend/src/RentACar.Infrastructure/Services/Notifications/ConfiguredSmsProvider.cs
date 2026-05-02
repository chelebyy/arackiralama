using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public sealed class ConfiguredSmsProvider(
    NetgsmSmsProvider netgsmSmsProvider,
    TwilioSmsProvider twilioSmsProvider,
    IOptions<NotificationOptions> notificationOptions) : ISmsProvider
{
    private readonly NotificationOptions _options = notificationOptions.Value;
    private static readonly string TwilioName = "Twilio";

    public async Task<SmsSendResult> SendAsync(
        SmsMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var primaryProvider = ResolveProvider(_options.Sms.PrimaryProvider);
        var primaryResult = await primaryProvider.SendAsync(request, cancellationToken);
        if (primaryResult.Success || !_options.Sms.EnableFallback)
        {
            return primaryResult;
        }

        var fallbackProvider = ResolveFallback(_options.Sms.PrimaryProvider);
        if (fallbackProvider is null)
        {
            return primaryResult;
        }

        var fallbackResult = await fallbackProvider.SendAsync(request, cancellationToken);
        return fallbackResult.Success ? fallbackResult : primaryResult;
    }

    private ISmsProvider ResolveProvider(string providerName)
    {
        return providerName.Equals(TwilioName, StringComparison.OrdinalIgnoreCase)
            ? twilioSmsProvider
            : netgsmSmsProvider;
    }

    private ISmsProvider? ResolveFallback(string providerName)
    {
        if (providerName.Equals(TwilioName, StringComparison.OrdinalIgnoreCase))
        {
            return netgsmSmsProvider;
        }

        return twilioSmsProvider;
    }
}
