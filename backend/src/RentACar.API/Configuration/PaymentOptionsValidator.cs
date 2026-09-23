using RentACar.Infrastructure.Services.Payments;

namespace RentACar.API.Configuration;

/// <summary>
/// Production-only fail-closed validator for <see cref="PaymentOptions"/>.
///
/// The repository's default configuration resolves to the Mock provider and
/// runs against sandbox URLs — that is acceptable for local development and
/// automated tests, but it must never be reachable from production. This
/// validator is run at startup (inside <c>AddPaymentIntegration</c>) so a
/// misconfigured production environment fails fast and visibly instead of
/// silently falling back to a development payment path that cannot enforce
/// real verification contracts.
/// </summary>
public static class PaymentOptionsValidator
{
    private static readonly string[] KnownProviders = { "Disabled", "Mock", "Iyzico" };

    public static void ValidateForProduction(PaymentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            throw new InvalidOperationException(
                "Payment provider is not configured. Production requires an explicit, supported provider.");
        }

        if (!KnownProviders.Contains(options.Provider, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unknown payment provider '{options.Provider}'. Production only allows: {string.Join(", ", KnownProviders)}.");
        }

        if (options.Provider.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            if (options.EnablePayments)
            {
                throw new InvalidOperationException(
                    "Disabled payment provider cannot process payments. Payment:EnablePayments must remain false.");
            }

            return;
        }

        if (options.Provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Mock payment provider is not allowed in production. Configure a real provider.");
        }

        if (string.IsNullOrWhiteSpace(options.Currency))
        {
            throw new InvalidOperationException("Payment currency is required in production.");
        }

        if (options.Iyzico is null
            || string.IsNullOrWhiteSpace(options.Iyzico.ApiKey)
            || string.IsNullOrWhiteSpace(options.Iyzico.SecretKey)
            || string.IsNullOrWhiteSpace(options.Iyzico.BaseUrl)
            || string.IsNullOrWhiteSpace(options.Iyzico.WebhookSecret))
        {
            throw new InvalidOperationException(
                "Iyzico payment provider is not fully configured in production: ApiKey, SecretKey, BaseUrl, and WebhookSecret are all required.");
        }

        if (options.Iyzico.BaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Iyzico BaseUrl must point to the production endpoint, not a sandbox URL.");
        }

        if (options.EnablePayments)
        {
            throw new InvalidOperationException(
                "The Iyzico payment provider is currently simulated and cannot be enabled in production. Payment:EnablePayments must remain false until a real provider integration is implemented.");
        }
    }

    public static bool IsValidForProduction(PaymentOptions options)
    {
        try
        {
            ValidateForProduction(options);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
