using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class MockPaymentProviderTests
{
    [Fact]
    public async Task CreatePaymentIntentAsync_WhenIdempotencyKeyContainsControlCharacters_LogsSanitizedValue()
    {
        var logger = new TestLogger<MockPaymentProvider>();
        var sut = CreateSut(logger: logger);

        await sut.CreatePaymentIntentAsync(CreatePaymentIntentRequest(idempotencyKey: "idem-123\r\nforged-entry"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("idem-123  forged-entry", entry.State["IdempotencyKey"]);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_WhenIdempotencyKeyContainsTimeout_ThrowsTimeoutException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            sut.CreatePaymentIntentAsync(CreatePaymentIntentRequest(idempotencyKey: "timeout-key")));
    }

    [Fact]
    public async Task CreatePreAuthorizationAsync_WhenAmountIsNotPositive_ReturnsFailedResult()
    {
        var sut = CreateSut();

        var result = await sut.CreatePreAuthorizationAsync(CreatePreAuthorizationRequest(amount: 0m));

        Assert.Equal(PaymentProviderIntentStatus.Failed, result.Status);
        Assert.Equal("MOCK_INVALID_DEPOSIT_AMOUNT", result.FailureCode);
        Assert.Equal("Deposit amount must be greater than zero.", result.FailureMessage);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task CreatePreAuthorizationAsync_WhenReferenceTransactionContainsTimeout_ThrowsTimeoutException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            sut.CreatePreAuthorizationAsync(CreatePreAuthorizationRequest(referenceTransactionId: "mock-timeout-ref")));
    }

    [Theory]
    [InlineData("bank-fail-response")]
    [InlineData("bank-cancel-response")]
    public async Task VerifyPaymentAsync_WhenBankResponseIndicatesFailure_ReturnsFailedResult(string bankResponse)
    {
        var sut = CreateSut();

        var result = await sut.VerifyPaymentAsync(new PaymentCallbackProviderRequest
        {
            ProviderIntentId = "mock-intent",
            BankResponse = bankResponse
        });

        Assert.Equal(PaymentProviderIntentStatus.Failed, result.Status);
        Assert.Equal("MOCK_3DS_FAILED", result.FailureCode);
        Assert.Equal("Mock provider marked payment as failed.", result.FailureMessage);
    }

    [Fact]
    public void VerifyWebhookSignature_WhenSignatureIsBlank_ReturnsFalse()
    {
        var sut = CreateSut();

        var isValid = sut.VerifyWebhookSignature("{}", " ", timestamp: null);

        Assert.False(isValid);
    }

    [Fact]
    public async Task ParseWebhookAsync_WhenExplicitEventTypeAndNumericFallbackFieldsProvided_PrefersExplicitEventType()
    {
        var sut = CreateSut();
        const string payload = """
        {
          "event_id": 12345,
          "payment_intent_id": "intent-42",
          "provider_transaction_id": 67890,
          "event_type": "payload-event"
        }
        """;

        var result = await sut.ParseWebhookAsync("mock", payload, eventType: "explicit-event");

        Assert.Equal("12345", result.ProviderEventId);
        Assert.Equal("explicit-event", result.EventType);
        Assert.Equal("intent-42", result.ProviderIntentId);
        Assert.Equal("67890", result.ProviderTransactionId);
        Assert.Equal(payload, result.RawPayload);
    }

    [Theory]
    [InlineData("mock-deposit-123", ProviderTransactionStatus.Authorized)]
    [InlineData("mock-fail-123", ProviderTransactionStatus.Failed)]
    [InlineData("mock-success-123", ProviderTransactionStatus.Succeeded)]
    public async Task GetTransactionStatusAsync_ReturnsExpectedStatus(string transactionId, ProviderTransactionStatus expectedStatus)
    {
        var sut = CreateSut();

        var result = await sut.GetTransactionStatusAsync(transactionId);

        Assert.Equal(expectedStatus, result);
    }

    [Fact]
    public async Task RefundAsync_WhenAmountIsNotPositive_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.RefundAsync(new ProviderRefundRequest
        {
            ProviderIntentId = "mock-transaction",
            Amount = 0m,
            Reason = "customer request"
        });

        Assert.False(result.Success);
        Assert.Equal("MOCK_INVALID_AMOUNT", result.FailureCode);
        Assert.Equal("Refund amount must be greater than zero.", result.FailureMessage);
    }

    [Fact]
    public async Task ReleaseDepositAsync_WhenProviderIntentIdContainsFail_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ReleaseDepositAsync(new ProviderReleaseDepositRequest
        {
            ProviderIntentId = "mock-fail-intent"
        });

        Assert.False(result.Success);
        Assert.Equal("MOCK_RELEASE_FAILED", result.FailureCode);
        Assert.Equal("Mock provider forced release failure.", result.FailureMessage);
    }

    [Fact]
    public async Task CaptureDepositAsync_WhenProviderIntentIdIsBlank_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.CaptureDepositAsync(new ProviderCaptureDepositRequest
        {
            ProviderIntentId = string.Empty,
            Amount = 100m
        });

        Assert.False(result.Success);
        Assert.Equal("MOCK_CAPTURE_INVALID_INTENT", result.FailureCode);
    }

    [Fact]
    public async Task CaptureDepositAsync_WhenAmountIsNotPositive_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.CaptureDepositAsync(new ProviderCaptureDepositRequest
        {
            ProviderIntentId = "mock-intent",
            Amount = 0m
        });

        Assert.False(result.Success);
        Assert.Equal("MOCK_CAPTURE_INVALID_AMOUNT", result.FailureCode);
    }

    [Fact]
    public async Task CaptureDepositAsync_WhenProviderIntentIdContainsFail_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.CaptureDepositAsync(new ProviderCaptureDepositRequest
        {
            ProviderIntentId = "mock-fail-intent",
            Amount = 100m
        });

        Assert.False(result.Success);
        Assert.Equal("MOCK_CAPTURE_FAILED", result.FailureCode);
    }

    private static MockPaymentProvider CreateSut(int intentExpiresMinutes = 15, TestLogger<MockPaymentProvider>? logger = null)
    {
        return new MockPaymentProvider(
            Options.Create(new PaymentOptions
            {
                IntentExpiresMinutes = intentExpiresMinutes,
                Mock = new MockProviderOptions
                {
                    WebhookSecret = "mock-secret"
                }
            }),
            logger ?? new TestLogger<MockPaymentProvider>());
    }

    private static CreatePaymentIntentProviderRequest CreatePaymentIntentRequest(
        string idempotencyKey = "idem-123",
        string holderName = "Test User")
    {
        return new CreatePaymentIntentProviderRequest
        {
            ReservationId = Guid.NewGuid(),
            Amount = 1000m,
            Currency = "TRY",
            IdempotencyKey = idempotencyKey,
            InstallmentCount = 1,
            Card = new ProviderCardData
            {
                HolderName = holderName,
                Number = "4111111111111111",
                ExpiryMonth = "12",
                ExpiryYear = "2030",
                Cvv = "123"
            }
        };
    }

    private static CreatePreAuthorizationProviderRequest CreatePreAuthorizationRequest(
        decimal amount = 500m,
        string referenceTransactionId = "mock-reference")
    {
        return new CreatePreAuthorizationProviderRequest
        {
            ReservationId = Guid.NewGuid(),
            Amount = amount,
            Currency = "TRY",
            ReferenceTransactionId = referenceTransactionId,
            IdempotencyKey = "preauth-idem"
        };
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>>;
            if (values == null)
            {
                return;
            }

            Entries.Add(new LogEntry(values.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty)));
        }

        public sealed record LogEntry(Dictionary<string, string> State);

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
