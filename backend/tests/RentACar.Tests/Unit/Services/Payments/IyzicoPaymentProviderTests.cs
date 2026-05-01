using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Services.Payments;

public sealed class IyzicoPaymentProviderTests
{
    private const string Payload = "{\"eventType\":\"PAYMENT_API\"}";
    private const string Secret = "test-webhook-secret";

    private readonly IyzicoPaymentProvider _sut = new(
        Options.Create(new PaymentOptions
        {
            Iyzico = new IyzicoProviderOptions
            {
                WebhookSecret = Secret
            }
        }),
        NullLogger<IyzicoPaymentProvider>.Instance);

    [Fact]
    public void VerifyWebhookSignature_WhenTimestampIsFreshUnixEpochSeconds_ReturnsTrue()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = CreateSignature(Payload, Secret);

        var result = _sut.VerifyWebhookSignature(Payload, signature, timestamp);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_WhenTimestampIsFreshIso8601_ReturnsTrue()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var signature = CreateSignature(Payload, Secret);

        var result = _sut.VerifyWebhookSignature(Payload, signature, timestamp);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_WhenTimestampIsOlderThanFiveMinutes_ReturnsFalse()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5).AddSeconds(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = CreateSignature(Payload, Secret);

        var result = _sut.VerifyWebhookSignature(Payload, signature, timestamp);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_WhenTimestampIsTooFarInTheFuture_ReturnsFalse()
    {
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(31).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = CreateSignature(Payload, Secret);

        var result = _sut.VerifyWebhookSignature(Payload, signature, timestamp);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-timestamp")]
    public void VerifyWebhookSignature_WhenTimestampIsInvalidOrMissing_ReturnsFalse(string? timestamp)
    {
        var signature = CreateSignature(Payload, Secret);

        var result = _sut.VerifyWebhookSignature(Payload, signature, timestamp);

        result.Should().BeFalse();
    }

    private static string CreateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
