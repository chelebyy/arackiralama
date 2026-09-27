using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PaymentThreeDsCallbackValidationTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("\"text\"")]
    public async Task NonObjectPayload_ReturnsFailureForBothProviders(string bankResponse)
    {
        foreach (var provider in CreateProviders())
        {
            var result = await provider.VerifyPaymentAsync(new PaymentCallbackProviderRequest
            {
                ProviderIntentId = "intent",
                BankResponse = bankResponse
            });

            Assert.Equal(PaymentProviderIntentStatus.Failed, result.Status);
        }
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(false, 0, true)]
    [InlineData(true, -600, false)]
    [InlineData(false, -600, false)]
    [InlineData(true, 120, false)]
    [InlineData(false, 120, false)]
    public async Task SignedTimestamp_RespectsFreshnessForBothFormats(bool numeric, int offset, bool accepted)
    {
        var instant = DateTimeOffset.UtcNow.AddSeconds(offset);
        object timestamp = numeric ? instant.ToUnixTimeSeconds() : instant.ToString("O", CultureInfo.InvariantCulture);
        var canonical = $"intent.success.transaction.{Convert.ToString(timestamp, CultureInfo.InvariantCulture)}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("test-callback-secret"), Encoding.UTF8.GetBytes(canonical)));
        var bankResponse = JsonSerializer.Serialize(new
        {
            providerIntentId = "intent",
            status = "success",
            transactionId = "transaction",
            timestamp,
            signature
        });

        foreach (var provider in CreateProviders())
        {
            var result = await provider.VerifyPaymentAsync(new PaymentCallbackProviderRequest
            {
                ProviderIntentId = "intent",
                BankResponse = bankResponse
            });

            Assert.Equal(accepted ? PaymentProviderIntentStatus.Succeeded : PaymentProviderIntentStatus.Failed, result.Status);
        }
    }

    private static IPaymentProvider[] CreateProviders()
    {
        var options = Options.Create(new PaymentOptions
        {
            Mock = new MockProviderOptions { WebhookSecret = "test-callback-secret" },
            Iyzico = new IyzicoProviderOptions { SecretKey = "test-callback-secret" }
        });
        return
        [
            new MockPaymentProvider(options, NullLogger<MockPaymentProvider>.Instance),
            new IyzicoPaymentProvider(options, NullLogger<IyzicoPaymentProvider>.Instance)
        ];
    }
}
