using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.API.Configuration;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Services.Payments;
using System.Reflection;
using Xunit;

namespace RentACar.Tests.Unit.Configuration;

public sealed class PaymentOptionsValidatorTests
{
    [Fact]
    public void ValidateForProduction_WithConfiguredIyzicoAndPaymentsEnabled_Fails()
    {
        var options = CreateConfiguredIyzicoOptions();

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*simulated*EnablePayments must remain false*");
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeFalse();
    }

    [Fact]
    public void ValidateForProduction_WithSafeIyzicoConfigurationAndPaymentsDisabled_Succeeds()
    {
        var options = CreateConfiguredIyzicoOptions();
        options.EnablePayments = false;

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().NotThrow();
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeTrue();
    }

    [Fact]
    public void ValidateForProduction_WithDisabledProviderAndPaymentsDisabled_Succeeds()
    {
        var options = CreateDisabledPaymentOptions();

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().NotThrow();
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeTrue();
    }

    [Fact]
    public void ValidateForProduction_WithDisabledProviderAndPaymentsEnabled_Fails()
    {
        var options = CreateDisabledPaymentOptions();
        options.EnablePayments = true;

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Payment:EnablePayments must remain false*");
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeFalse();
    }

    [Theory]
    [InlineData("Mock", true, "https://api.iyzipay.com")]
    [InlineData("Iyzico", true, "https://sandbox-api.iyzipay.com")]
    [InlineData("Unknown", true, "https://api.iyzipay.com")]
    public void ValidateForProduction_WithUnsafeConfiguration_Fails(
        string provider,
        bool enablePayments,
        string baseUrl)
    {
        var options = CreateConfiguredIyzicoOptions();
        options.Provider = provider;
        options.EnablePayments = enablePayments;
        options.Iyzico.BaseUrl = baseUrl;

        var action = () => PaymentOptionsValidator.ValidateForProduction(options);

        action.Should().Throw<InvalidOperationException>();
        PaymentOptionsValidator.IsValidForProduction(options).Should().BeFalse();
    }

    [Theory]
    [InlineData("Payment:Provider", "")]
    [InlineData("Payment:Provider", "Mock")]
    [InlineData("Payment:Provider", "Unknown")]
    [InlineData("Payment:Iyzico:BaseUrl", "https://sandbox-api.iyzipay.com")]
    [InlineData("Payment:Iyzico:ApiKey", "")]
    public async Task ProductionStartup_WithUnsafePaymentConfiguration_Fails(
        string configurationKey,
        string configurationValue)
    {
        var configuration = CreateConfiguredIyzicoConfiguration();
        configuration[configurationKey] = configurationValue;
        using var host = CreatePaymentHost(Environments.Production, configuration);

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Production payment configuration is incomplete or unsafe.*");
    }

    [Fact]
    public async Task ProductionStartup_WithConfiguredIyzicoAndPaymentsEnabled_Fails()
    {
        using var host = CreatePaymentHost(
            Environments.Production,
            CreateConfiguredIyzicoConfiguration());

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Production payment configuration is incomplete or unsafe.*");
    }

    [Fact]
    public async Task ProductionStartup_WithSafePaymentConfigurationAndPaymentsDisabled_Succeeds()
    {
        var configuration = CreateConfiguredIyzicoConfiguration();
        configuration["Payment:EnablePayments"] = "false";
        using var host = CreatePaymentHost(Environments.Production, configuration);

        var action = () => host.StartAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProductionStartup_WithDisabledProviderAndPaymentsDisabled_Succeeds()
    {
        using var host = CreatePaymentHost(
            Environments.Production,
            CreateDisabledPaymentConfiguration());

        var action = () => host.StartAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisabledProvider_ResolvesExplicitlyAndFailsClosed()
    {
        using var host = CreatePaymentHost(
            Environments.Production,
            CreateDisabledPaymentConfiguration());
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();

        provider.GetType().Name.Should().Be("DisabledPaymentProvider");

        var intent = await provider.CreatePaymentIntentAsync(new CreatePaymentIntentProviderRequest());
        intent.Status.Should().Be(PaymentProviderIntentStatus.Failed);
        intent.ProviderIntentId.Should().BeEmpty();
        intent.RedirectUrl.Should().BeNull();

        var preAuthorization = await provider.CreatePreAuthorizationAsync(new CreatePreAuthorizationProviderRequest());
        preAuthorization.Status.Should().Be(PaymentProviderIntentStatus.Failed);
        preAuthorization.FailureCode.Should().Be("PAYMENTS_DISABLED");

        var verification = await provider.VerifyPaymentAsync(new PaymentCallbackProviderRequest());
        verification.Status.Should().Be(PaymentProviderIntentStatus.Failed);
        verification.FailureCode.Should().Be("PAYMENTS_DISABLED");

        provider.VerifyWebhookSignature("payload", "signature", null).Should().BeFalse();
        await FluentActions.Invoking(() => provider.ParseWebhookAsync("Disabled", "{}", null))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payment processing is disabled.");

        var transactionStatus = await provider.GetTransactionStatusAsync("transaction-id");
        transactionStatus.Should().Be(ProviderTransactionStatus.Unknown);

        var refund = await provider.RefundAsync(new ProviderRefundRequest());
        refund.Success.Should().BeFalse();
        refund.FailureCode.Should().Be("PAYMENTS_DISABLED");

        var release = await provider.ReleaseDepositAsync(new ProviderReleaseDepositRequest());
        release.Success.Should().BeFalse();
        release.FailureCode.Should().Be("PAYMENTS_DISABLED");

        var capture = await provider.CaptureDepositAsync(new ProviderCaptureDepositRequest());
        capture.Success.Should().BeFalse();
        capture.FailureCode.Should().Be("PAYMENTS_DISABLED");
    }

    [Fact]
    public async Task DevelopmentStartup_WithExplicitMockProviderAndPaymentsDisabled_Succeeds()
    {
        var configuration = CreateConfiguredIyzicoConfiguration();
        configuration["Payment:Provider"] = "Mock";
        configuration["Payment:EnablePayments"] = "false";
        configuration["Payment:Iyzico:BaseUrl"] = "https://sandbox-api.iyzipay.com";
        using var host = CreatePaymentHost(Environments.Development, configuration);

        var action = () => host.StartAsync();

        await action.Should().NotThrowAsync();
    }

    private static IHost CreatePaymentHost(
        string environmentName,
        IReadOnlyDictionary<string, string?> configuration)
    {
        return Host.CreateDefaultBuilder()
            .UseEnvironment(environmentName)
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
            .ConfigureServices((context, services) =>
            {
                var registrationMethod = typeof(ServiceCollectionExtensions).GetMethod(
                    "AddPaymentIntegration",
                    BindingFlags.NonPublic | BindingFlags.Static);

                registrationMethod.Should().NotBeNull();
                registrationMethod!.Invoke(
                    null,
                    new object[] { services, context.Configuration, context.HostingEnvironment });
            })
            .Build();
    }

    private static Dictionary<string, string?> CreateConfiguredIyzicoConfiguration() => new()
    {
        ["Payment:Provider"] = "Iyzico",
        ["Payment:Currency"] = "TRY",
        ["Payment:EnablePayments"] = "true",
        ["Payment:Iyzico:ApiKey"] = "test-api-key",
        ["Payment:Iyzico:SecretKey"] = "test-secret-key",
        ["Payment:Iyzico:BaseUrl"] = "https://api.iyzipay.com",
        ["Payment:Iyzico:WebhookSecret"] = "test-webhook-secret"
    };

    private static Dictionary<string, string?> CreateDisabledPaymentConfiguration() => new()
    {
        ["Payment:Provider"] = "Disabled",
        ["Payment:Currency"] = "TRY",
        ["Payment:EnablePayments"] = "false"
    };

    private static PaymentOptions CreateConfiguredIyzicoOptions() => new()
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

    private static PaymentOptions CreateDisabledPaymentOptions() => new()
    {
        Provider = "Disabled",
        Currency = "TRY",
        EnablePayments = false
    };
}
