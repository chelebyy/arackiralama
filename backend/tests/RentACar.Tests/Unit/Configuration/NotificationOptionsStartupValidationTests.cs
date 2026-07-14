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
    public async Task Startup_WithUnsafePublicFrontendBaseUrl_Fails(string publicFrontendBaseUrl)
    {
        using var host = CreateNotificationHost(publicFrontendBaseUrl);

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Notifications:PublicFrontendBaseUrl*");
    }

    [Theory]
    [InlineData("http://localhost:3001")]
    [InlineData("https://rental.example.test/base/")]
    public async Task Startup_WithSafePublicFrontendBaseUrl_Succeeds(string publicFrontendBaseUrl)
    {
        using var host = CreateNotificationHost(publicFrontendBaseUrl);

        var action = () => host.StartAsync();

        await action.Should().NotThrowAsync();
    }

    private static IHost CreateNotificationHost(string publicFrontendBaseUrl)
    {
        return Host.CreateDefaultBuilder()
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
                    new object[] { services, context.Configuration });
            })
            .Build();
    }
}
