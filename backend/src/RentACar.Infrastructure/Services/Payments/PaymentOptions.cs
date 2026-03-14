namespace RentACar.Infrastructure.Services.Payments;

public sealed class PaymentOptions
{
    public string Provider { get; init; } = "Mock";
    public string Currency { get; init; } = "TRY";
    public int IntentExpiresMinutes { get; init; } = 15;
    public int RetryLimit { get; init; } = 3;
    public int TimeoutRetryCount { get; init; } = 2;
    public int WebhookJobBatchSize { get; init; } = 20;
    public MockProviderOptions Mock { get; init; } = new();
    public IyzicoProviderOptions Iyzico { get; init; } = new();
}

public sealed class MockProviderOptions
{
    public string WebhookSecret { get; init; } = "mock-webhook-secret";
}

public sealed class IyzicoProviderOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://sandbox-api.iyzipay.com";
    public string WebhookSecret { get; init; } = string.Empty;
}
