using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RentACar.API.Configuration;
using RentACar.Infrastructure.Services.Payments;
using System.Reflection;
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
        var configuration = CreateSafeConfiguration();
        configuration[configurationKey] = configurationValue;
        using var host = CreatePaymentHost(Environments.Production, configuration);

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Production payment configuration is incomplete or unsafe.*");
    }

    [Fact]
    public async Task ProductionStartup_WithSafePaymentConfiguration_Succeeds()
    {
        using var host = CreatePaymentHost(Environments.Production, CreateSafeConfiguration());

        var action = () => host.StartAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DevelopmentStartup_WithExplicitMockProviderAndPaymentsDisabled_Succeeds()
    {
        var configuration = CreateSafeConfiguration();
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

    private static Dictionary<string, string?> CreateSafeConfiguration() => new()
    {
        ["Payment:Provider"] = "Iyzico",
        ["Payment:Currency"] = "TRY",
        ["Payment:EnablePayments"] = "true",
        ["Payment:Iyzico:ApiKey"] = "test-api-key",
        ["Payment:Iyzico:SecretKey"] = "test-secret-key",
        ["Payment:Iyzico:BaseUrl"] = "https://api.iyzipay.com",
        ["Payment:Iyzico:WebhookSecret"] = "test-webhook-secret"
    };

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
