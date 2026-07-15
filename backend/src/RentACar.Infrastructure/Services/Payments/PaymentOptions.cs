namespace RentACar.Infrastructure.Services.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Provider { get; set; } = "Mock";
    public string Currency { get; set; } = "TRY";
    public int IntentExpiresMinutes { get; init; } = 15;
    public int RetryLimit { get; init; } = 3;
    public int TimeoutRetryCount { get; init; } = 2;
    public int WebhookJobBatchSize { get; init; } = 20;
    public MockProviderOptions Mock { get; init; } = new();
    public IyzicoProviderOptions Iyzico { get; init; } = new();

    // Emergency containment kill switch (WP0). When false, payment intent
    // creation, 3DS completion, and webhook endpoints return 503 without mutating state.
    // Defaults to false until a real payment provider is selected and configured.
    public bool EnablePayments { get; set; }
}

public sealed class MockProviderOptions
{
    public string WebhookSecret { get; init; } = "mock-webhook-secret";
}

public sealed class IyzicoProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";
    public string WebhookSecret { get; set; } = string.Empty;
}
