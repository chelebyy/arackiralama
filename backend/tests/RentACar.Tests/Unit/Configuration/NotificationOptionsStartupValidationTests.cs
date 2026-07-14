using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RentACar.API.Configuration;
using Xunit;

namespace RentACar.Tests.Unit.Configuration;

public sealed class NotificationOptionsStartupValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/tr")]
    [InlineData("ftp://rental.example.test")]
    [InlineData("https://rental.example.test?source=claim")]
    [InlineData("https://rental.example.test/#claim")]
    [InlineData("http://rental.example.test")]
    [InlineData("http://localhost:3001")]
    public async Task Startup_WithUnsafePublicFrontendBaseUrl_Fails(string publicFrontendBaseUrl)
    {
        using var host = CreateNotificationHost(Environments.Production, publicFrontendBaseUrl);

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Notifications:PublicFrontendBaseUrl*");
    }

    [Theory]
    [InlineData("Development", "http://localhost:3001")]
    [InlineData("Development", "http://127.0.0.1:3001")]
    [InlineData("Production", "https://rental.example.test/base/")]
    public async Task Startup_WithSafePublicFrontendBaseUrl_Succeeds(
        string environmentName,
        string publicFrontendBaseUrl)
    {
        using var host = CreateNotificationHost(environmentName, publicFrontendBaseUrl);

        var action = () => host.StartAsync();

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DevelopmentStartup_WithRemoteHttpPublicFrontendBaseUrl_Fails()
    {
        using var host = CreateNotificationHost(
            Environments.Development,
            "http://rental.example.test");

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Notifications:PublicFrontendBaseUrl*");
    }

    private static IHost CreateNotificationHost(
        string environmentName,
        string publicFrontendBaseUrl)
    {
        return Host.CreateDefaultBuilder()
            .UseEnvironment(environmentName)
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:PublicFrontendBaseUrl"] = publicFrontendBaseUrl
            }))
            .ConfigureServices((context, services) =>
            {
                var registrationMethod = typeof(ServiceCollectionExtensions).GetMethod(
                    "AddNotificationOptions",
                    BindingFlags.NonPublic | BindingFlags.Static);

                registrationMethod.Should().NotBeNull();
                registrationMethod!.Invoke(
                    null,
                    new object[] { services, context.Configuration, context.HostingEnvironment });
            })
            .Build();
    }
}
