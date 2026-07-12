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
    [InlineData("tr-TR", "/tr/account-claim?token=claim-token")]
    [InlineData("en-US", "/en/account-claim?token=claim-token")]
    [InlineData("de-DE", "/de/account-claim?token=claim-token")]
    public async Task DispatchAsync_UsesSupportedPublicLocaleRoute(string locale, string expectedUrl)
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
            Options.Create(new NotificationOptions { DefaultLocale = "tr-TR" }),
            Mock.Of<ILogger<CustomerAccountClaimEmailDispatcher>>());

        await dispatcher.DispatchAsync(
            "customer@example.test",
            "claim-token",
            DateTime.UtcNow.AddHours(1),
            locale);

        queuedRequest.Should().NotBeNull();
        queuedRequest!.Variables["ClaimUrl"].Should().Be(expectedUrl);
    }
}
