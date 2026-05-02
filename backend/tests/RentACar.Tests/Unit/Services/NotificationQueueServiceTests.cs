using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class NotificationQueueServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACar.Infrastructure.Data.RentACarDbContext _dbContext;

    public NotificationQueueServiceTests()
    {
        _dbContext = _dbFactory.CreateContext();
    }

    [Fact]
    public async Task EnqueueEmailAsync_WhenCalled_CreatesPendingBackgroundJob()
    {
        var sut = new NotificationQueueService(_dbContext, NullLogger<NotificationQueueService>.Instance);

        var jobId = await sut.EnqueueEmailAsync(new QueuedEmailNotificationRequest
        {
            ToEmail = "customer@example.com",
            TemplateKey = NotificationTemplateKeys.PaymentReceived,
            Locale = "tr-TR",
            Variables = new Dictionary<string, string>
            {
                ["PublicCode"] = "RSV-001"
            }
        });

        var job = _dbContext.BackgroundJobs.Single(x => x.Id == jobId);
        job.Type.Should().Be(NotificationQueueService.SendEmailJobType);
        job.Status.Should().Be(RentACar.Core.Enums.BackgroundJobStatus.Pending);
        job.Payload.Should().Contain("customer@example.com");
        job.Payload.Should().Contain(NotificationTemplateKeys.PaymentReceived);
    }

    [Fact]
    public async Task EnqueueSmsAsync_WhenScheduledAtProvided_PersistsScheduledTime()
    {
        var sut = new NotificationQueueService(_dbContext, NullLogger<NotificationQueueService>.Instance);
        var scheduledAtUtc = DateTime.UtcNow.AddHours(6);

        var jobId = await sut.EnqueueSmsAsync(
            new QueuedSmsNotificationRequest
            {
                ToPhoneNumber = "+905551112233",
                TemplateKey = NotificationTemplateKeys.PickupReminder,
                Locale = "tr-TR",
                Variables = new Dictionary<string, string>
                {
                    ["PublicCode"] = "RSV-001"
                }
            },
            scheduledAtUtc,
            CancellationToken.None);

        var job = _dbContext.BackgroundJobs.Single(x => x.Id == jobId);
        job.Type.Should().Be(NotificationQueueService.SendSmsJobType);
        job.ScheduledAt.Should().Be(scheduledAtUtc);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }
}
