using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RentACar.API.Services;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PasswordResetEmailDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WhenCalled_ComposesPasswordResetEmail()
    {
        var fakeQueueService = new FakeNotificationQueueService();
        var sut = new PasswordResetEmailDispatcher(fakeQueueService, NullLogger<PasswordResetEmailDispatcher>.Instance);
        var expiresAtUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc);

        await sut.DispatchAsync(
            AuthPrincipalType.Customer,
            "customer@example.com",
            "reset-token-123",
            expiresAtUtc,
            CancellationToken.None);

        fakeQueueService.LastEmailRequest.Should().NotBeNull();
        fakeQueueService.LastEmailRequest!.ToEmail.Should().Be("customer@example.com");
        fakeQueueService.LastEmailRequest.TemplateKey.Should().Be(NotificationTemplateKeys.PasswordResetCustomer);
        fakeQueueService.LastEmailRequest.Locale.Should().Be("tr-TR");
        fakeQueueService.LastEmailRequest.Variables["Token"].Should().Be("reset-token-123");
        fakeQueueService.LastEmailRequest.Variables["ExpiresAtUtc"].Should().Be(expiresAtUtc.ToString("u"));
    }

    [Fact]
    public async Task DispatchAsync_WhenCalled_QueuesEmailWithoutThrowing()
    {
        var fakeQueueService = new FakeNotificationQueueService();
        var sut = new PasswordResetEmailDispatcher(fakeQueueService, NullLogger<PasswordResetEmailDispatcher>.Instance);

        var action = () => sut.DispatchAsync(
            AuthPrincipalType.Admin,
            "admin@example.com",
            "token",
            DateTime.UtcNow,
            CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    private sealed class FakeNotificationQueueService : INotificationQueueService
    {
        public QueuedEmailNotificationRequest? LastEmailRequest { get; private set; }

        public Task<Guid> EnqueueEmailAsync(QueuedEmailNotificationRequest request, DateTime? scheduledAtUtc = null, CancellationToken cancellationToken = default)
        {
            LastEmailRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid> EnqueueSmsAsync(QueuedSmsNotificationRequest request, DateTime? scheduledAtUtc = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
