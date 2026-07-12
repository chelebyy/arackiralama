using FluentAssertions;
using RentACar.API.Configuration;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Configuration;

public sealed class PaymentOptionsValidatorTests
{
    [Fact]
    public void ValidateForProduction_WithSafeIyzicoConfiguration_Succeeds()
    {
        var options = CreateSafeOptions();

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().NotThrow();
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeTrue();
    }

    [Theory]
    [InlineData("Mock", true, "https://api.iyzipay.com")]
    [InlineData("Iyzico", false, "https://api.iyzipay.com")]
    [InlineData("Iyzico", true, "https://sandbox-api.iyzipay.com")]
    [InlineData("Unknown", true, "https://api.iyzipay.com")]
    public void ValidateForProduction_WithUnsafeConfiguration_Fails(
        string provider,
        bool enablePayments,
        string baseUrl)
    {
        var options = CreateSafeOptions();
        options.Provider = provider;
        options.EnablePayments = enablePayments;
        options.Iyzico.BaseUrl = baseUrl;

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().Throw<InvalidOperationException>();
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeFalse();
    }

    private static PaymentOptions CreateSafeOptions() => new()
    {
        Provider = "Iyzico",
        Currency = "TRY",
        EnablePayments = true,
        Iyzico = new IyzicoProviderOptions
        {
            ApiKey = "test-api-key",
            SecretKey = "test-secret-key",
            BaseUrl = "https://api.iyzipay.com",
            WebhookSecret = "test-webhook-secret"
        }
    };
}
