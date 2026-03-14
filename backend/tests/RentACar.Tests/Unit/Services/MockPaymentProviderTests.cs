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
        var sut = new MockPaymentProvider(
            Options.Create(new PaymentOptions
            {
                IntentExpiresMinutes = 15
            }),
            logger);

        await sut.CreatePaymentIntentAsync(
            new CreatePaymentIntentProviderRequest
            {
                ReservationId = Guid.NewGuid(),
                Amount = 1000m,
                Currency = "TRY",
                IdempotencyKey = "idem-123\r\nforged-entry",
                InstallmentCount = 1,
                Card = new ProviderCardData
                {
                    HolderName = "Test User",
                    Number = "4111111111111111",
                    ExpiryMonth = "12",
                    ExpiryYear = "2030",
                    Cvv = "123"
                }
            });

        var entry = Assert.Single(logger.Entries);
        // Control characters are replaced with spaces to preserve log line structure
        Assert.Equal("idem-123  forged-entry", entry.State["IdempotencyKey"]);
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
