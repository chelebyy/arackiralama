using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ConfiguredSmsProviderTests
{
    [Fact]
    public async Task SendAsync_WhenPrimaryProviderFails_UsesFallbackProvider()
    {
        var request = CreateRequest();
        var netgsmProvider = CreateNetgsmProvider(success: false, provider: "Netgsm");
        var twilioProvider = CreateTwilioProvider(success: true, provider: "Twilio");
        var sut = CreateSut(netgsmProvider, twilioProvider, primaryProvider: "Netgsm", enableFallback: true);

        var result = await sut.SendAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Twilio");
        netgsmProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        twilioProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenPrimaryProviderSucceeds_DoesNotUseFallback()
    {
        var request = CreateRequest();
        var netgsmProvider = CreateNetgsmProvider(success: true, provider: "Netgsm");
        var twilioProvider = CreateTwilioProvider(success: true, provider: "Twilio");
        var sut = CreateSut(netgsmProvider, twilioProvider, primaryProvider: "Netgsm", enableFallback: true);

        var result = await sut.SendAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Netgsm");
        netgsmProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        twilioProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenFallbackIsDisabled_ReturnsPrimaryFailureWithoutCallingFallback()
    {
        var request = CreateRequest();
        var netgsmProvider = CreateNetgsmProvider(success: false, provider: "Netgsm");
        var twilioProvider = CreateTwilioProvider(success: true, provider: "Twilio");
        var sut = CreateSut(netgsmProvider, twilioProvider, primaryProvider: "Netgsm", enableFallback: false);

        var result = await sut.SendAsync(request);

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Netgsm");
        netgsmProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        twilioProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenFallbackAlsoFails_ReturnsPrimaryFailure()
    {
        var request = CreateRequest();
        var netgsmProvider = CreateNetgsmProvider(success: false, provider: "Netgsm");
        var twilioProvider = CreateTwilioProvider(success: false, provider: "Twilio");
        var sut = CreateSut(netgsmProvider, twilioProvider, primaryProvider: "Netgsm", enableFallback: true);

        var result = await sut.SendAsync(request);

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Netgsm");
        netgsmProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        twilioProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTwilioIsPrimaryAndFails_UsesNetgsmAsFallback()
    {
        var request = CreateRequest();
        var netgsmProvider = CreateNetgsmProvider(success: true, provider: "Netgsm");
        var twilioProvider = CreateTwilioProvider(success: false, provider: "Twilio");
        var sut = CreateSut(netgsmProvider, twilioProvider, primaryProvider: "Twilio", enableFallback: true);

        var result = await sut.SendAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Netgsm");
        twilioProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        netgsmProvider.Verify(x => x.SendAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SmsMessageRequest CreateRequest()
    {
        return new SmsMessageRequest
        {
            ToPhoneNumber = "+905551112233",
            Body = "test"
        };
    }

    private static ConfiguredSmsProvider CreateSut(
        Mock<NetgsmSmsProvider> netgsmProvider,
        Mock<TwilioSmsProvider> twilioProvider,
        string primaryProvider,
        bool enableFallback)
    {
        return new ConfiguredSmsProvider(
            netgsmProvider.Object,
            twilioProvider.Object,
            Options.Create(new NotificationOptions
            {
                Sms = new SmsNotificationOptions
                {
                    PrimaryProvider = primaryProvider,
                    EnableFallback = enableFallback
                }
            }));
    }

    private static Mock<NetgsmSmsProvider> CreateNetgsmProvider(bool success, string provider)
    {
        var mock = new Mock<NetgsmSmsProvider>(
            new HttpClient(),
            Options.Create(new NotificationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NetgsmSmsProvider>.Instance);
        mock.Setup(x => x.SendAsync(It.IsAny<SmsMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsSendResult { Success = success, Provider = provider });
        return mock;
    }

    private static Mock<TwilioSmsProvider> CreateTwilioProvider(bool success, string provider)
    {
        var mock = new Mock<TwilioSmsProvider>(
            new HttpClient(),
            Options.Create(new NotificationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TwilioSmsProvider>.Instance);
        mock.Setup(x => x.SendAsync(It.IsAny<SmsMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsSendResult { Success = success, Provider = provider });
        return mock;
    }
}
