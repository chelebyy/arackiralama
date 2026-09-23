using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RentACar.API.Services;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class CustomerAccountClaimEmailDispatcherTests
{
    [Theory]
    [InlineData("tr-TR", "https://rental.example.test/tr/account-claim#token=claim-token")]
    [InlineData("en-US", "https://rental.example.test/en/account-claim#token=claim-token")]
    [InlineData("de-DE", "https://rental.example.test/de/account-claim#token=claim-token")]
    public async Task DispatchAsync_UsesAbsoluteSupportedPublicLocaleRoute(string locale, string expectedUrl)
    {
        var queue = new Mock<INotificationQueueService>();
        QueuedEmailNotificationRequest? queuedRequest = null;
        queue
            .Setup(service => service.EnqueueEmailAsync(
                It.IsAny<QueuedEmailNotificationRequest>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<QueuedEmailNotificationRequest, DateTime?, CancellationToken>((request, _, _) => queuedRequest = request)
            .ReturnsAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var dispatcher = new CustomerAccountClaimEmailDispatcher(
            queue.Object,
            Options.Create(new NotificationOptions
            {
                DefaultLocale = "tr-TR",
                PublicFrontendBaseUrl = "https://rental.example.test"
            }),
            Mock.Of<ILogger<CustomerAccountClaimEmailDispatcher>>());

        await dispatcher.DispatchAsync(
            "customer@example.test",
            "claim-token",
            DateTime.UtcNow.AddHours(1),
            locale);

        queuedRequest.Should().NotBeNull();
        queuedRequest!.Variables["ClaimUrl"].Should().Be(expectedUrl);
    }

    [Fact]
    public async Task DispatchAsync_WithRelativePublicFrontendBaseUrl_RejectsConfiguration()
    {
        var dispatcher = new CustomerAccountClaimEmailDispatcher(
            Mock.Of<INotificationQueueService>(),
            Options.Create(new NotificationOptions { PublicFrontendBaseUrl = "/tr" }),
            Mock.Of<ILogger<CustomerAccountClaimEmailDispatcher>>());
        var action = () => dispatcher.DispatchAsync(
            "customer@example.test",
            "claim-token",
            DateTime.UtcNow.AddHours(1),
            "tr-TR");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Notifications:PublicFrontendBaseUrl*");
    }
}
