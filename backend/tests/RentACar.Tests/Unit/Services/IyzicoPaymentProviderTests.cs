using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class IyzicoPaymentProviderTests
{
    [Fact]
    public async Task CreatePaymentIntentAsync_DoesNotUseTimeoutTriggerStrings()
    {
        var sut = CreateSut();

        var result = await sut.CreatePaymentIntentAsync(new CreatePaymentIntentProviderRequest
        {
            ReservationId = Guid.NewGuid(),
            Amount = 1000m,
            Currency = "TRY",
            IdempotencyKey = "booking-timeout-key",
            InstallmentCount = 1,
            Card = new ProviderCardData
            {
                HolderName = "Timeout Traveler",
                Number = "4111111111111111",
                ExpiryMonth = "12",
                ExpiryYear = "2030",
                Cvv = "123"
            }
        });

        Assert.Equal(PaymentProviderIntentStatus.Pending3DS, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderIntentId));
    }

    [Fact]
    public async Task CreatePreAuthorizationAsync_DoesNotUseTimeoutTriggerStrings()
    {
        var sut = CreateSut();

        var result = await sut.CreatePreAuthorizationAsync(new CreatePreAuthorizationProviderRequest
        {
            ReservationId = Guid.NewGuid(),
            Amount = 500m,
            Currency = "TRY",
            ReferenceTransactionId = "provider-timeout-reference",
            IdempotencyKey = "idem-1"
        });

        Assert.Equal(PaymentProviderIntentStatus.Authorized, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderIntentId));
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("fail")]
    [InlineData("cancel")]
    [InlineData("timeout-fail-cancel")]
    public async Task VerifyPaymentAsync_DoesNotUseBankResponseTriggerStrings(string bankResponse)
    {
        var sut = CreateSut();

        var result = await sut.VerifyPaymentAsync(new PaymentCallbackProviderRequest
        {
            ProviderIntentId = "intent-1",
            BankResponse = bankResponse,
            RawPayload = "{}"
        });

        Assert.Equal(PaymentProviderIntentStatus.Succeeded, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.TransactionId));
    }

    [Theory]
    [InlineData("preauth-transaction")]
    [InlineData("deposit-transaction")]
    [InlineData("failed-transaction")]
    public async Task GetTransactionStatusAsync_DoesNotInferStatusFromTransactionText(string transactionId)
    {
        var sut = CreateSut();

        var result = await sut.GetTransactionStatusAsync(transactionId);

        Assert.Equal(ProviderTransactionStatus.Unknown, result);
    }

    [Fact]
    public async Task RefundAsync_DoesNotUseProviderIntentFailureTriggerStrings()
    {
        var sut = CreateSut();

        var result = await sut.RefundAsync(new ProviderRefundRequest
        {
            ProviderIntentId = "intent-fail-marker",
            Amount = 250m,
            Currency = "TRY",
            Reason = "Customer requested refund"
        });

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ReferenceId));
    }

    [Fact]
    public async Task ReleaseDepositAsync_DoesNotUseProviderIntentFailureTriggerStrings()
    {
        var sut = CreateSut();

        var result = await sut.ReleaseDepositAsync(new ProviderReleaseDepositRequest
        {
            ProviderIntentId = "intent-fail-marker",
            Note = "Release deposit"
        });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CaptureDepositAsync_DoesNotUseProviderIntentFailureTriggerStrings()
    {
        var sut = CreateSut();

        var result = await sut.CaptureDepositAsync(new ProviderCaptureDepositRequest
        {
            ProviderIntentId = "intent-fail-marker",
            Amount = 125m,
            Currency = "TRY",
            Note = "Capture deposit"
        });

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ReferenceId));
    }

    private static IyzicoPaymentProvider CreateSut()
    {
        return new IyzicoPaymentProvider(
            Options.Create(new PaymentOptions
            {
                IntentExpiresMinutes = 15,
                Iyzico = new IyzicoProviderOptions
                {
                    BaseUrl = "https://sandbox-api.iyzipay.com"
                }
            }),
            NullLogger<IyzicoPaymentProvider>.Instance);
    }
}
