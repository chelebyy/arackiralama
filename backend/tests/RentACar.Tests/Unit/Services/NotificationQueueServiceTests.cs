using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RentACar.Core.Constants;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
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

    [Fact]
    public async Task EnqueueEmailAsync_WhenScheduledAtNotProvided_DefaultsScheduledAtToCurrentUtc()
    {
        var sut = new NotificationQueueService(_dbContext, NullLogger<NotificationQueueService>.Instance);
        var beforeUtc = DateTime.UtcNow;

        var jobId = await sut.EnqueueEmailAsync(new QueuedEmailNotificationRequest
        {
            ToEmail = "customer@example.com",
            TemplateKey = NotificationTemplateKeys.PaymentReceived,
            Locale = "tr-TR",
            Variables = new Dictionary<string, string>
            {
                ["PublicCode"] = "RSV-DEFAULT"
            }
        });

        var afterUtc = DateTime.UtcNow;
        var job = _dbContext.BackgroundJobs.Single(x => x.Id == jobId);
        job.ScheduledAt.Should().BeOnOrAfter(beforeUtc);
        job.ScheduledAt.Should().BeOnOrBefore(afterUtc);
    }

    [Fact]
    public async Task EnqueueSmsAsync_WhenSmsFeatureFlagDisabled_ReturnsEmptyAndDoesNotCreateBackgroundJob()
    {
        _dbContext.FeatureFlags.Add(new RentACar.Core.Entities.FeatureFlag
        {
            Name = NotificationConstants.EnableSmsNotificationsFlag,
            Enabled = false
        });
        await _dbContext.SaveChangesAsync();

        var sut = new NotificationQueueService(_dbContext, NullLogger<NotificationQueueService>.Instance);

        var jobId = await sut.EnqueueSmsAsync(new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+905551112233",
            TemplateKey = NotificationTemplateKeys.PickupReminder,
            Locale = "tr-TR",
            Variables = new Dictionary<string, string>
            {
                ["PublicCode"] = "RSV-DISABLED"
            }
        });

        jobId.Should().Be(Guid.Empty);
        _dbContext.BackgroundJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueSmsAsync_WhenSmsFeatureFlagQueryThrows_LogsWarningAndStillCreatesBackgroundJob()
    {
        var loggerMock = new Mock<ILogger<NotificationQueueService>>();
        var dbContextMock = new Mock<IApplicationDbContext>();
        var expectedException = new InvalidOperationException("feature flag query failed");

        dbContextMock.SetupGet(x => x.BackgroundJobs).Returns(_dbContext.BackgroundJobs);
        dbContextMock.SetupGet(x => x.FeatureFlags).Throws(expectedException);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(cancellationToken => _dbContext.SaveChangesAsync(cancellationToken));

        var sut = new NotificationQueueService(dbContextMock.Object, loggerMock.Object);

        var jobId = await sut.EnqueueSmsAsync(new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+905551112233",
            TemplateKey = NotificationTemplateKeys.PickupReminder,
            Locale = "tr-TR",
            Variables = new Dictionary<string, string>
            {
                ["PublicCode"] = "RSV-WARN"
            }
        });

        jobId.Should().NotBe(Guid.Empty);
        var job = _dbContext.BackgroundJobs.Single(x => x.Id == jobId);
        job.Type.Should().Be(NotificationQueueService.SendSmsJobType);
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to query SMS feature flag")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueSmsAsync_WhenSmsFeatureFlagEnabled_CreatesPendingSmsBackgroundJob()
    {
        _dbContext.FeatureFlags.Add(new RentACar.Core.Entities.FeatureFlag
        {
            Name = NotificationConstants.EnableSmsNotificationsFlag,
            Enabled = true
        });
        await _dbContext.SaveChangesAsync();

        var sut = new NotificationQueueService(_dbContext, NullLogger<NotificationQueueService>.Instance);

        var jobId = await sut.EnqueueSmsAsync(new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+905551112233",
            TemplateKey = NotificationTemplateKeys.PickupReminder,
            Locale = "tr-TR",
            Variables = new Dictionary<string, string>
            {
                ["PublicCode"] = "RSV-ENABLED"
            }
        });

        jobId.Should().NotBe(Guid.Empty);
        var job = _dbContext.BackgroundJobs.Single(x => x.Id == jobId);
        job.Type.Should().Be(NotificationQueueService.SendSmsJobType);
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        var payload = System.Text.Json.JsonSerializer.Deserialize<QueuedSmsNotificationRequest>(job.Payload);
        payload.Should().NotBeNull();
        payload!.ToPhoneNumber.Should().Be("+905551112233");
        payload.TemplateKey.Should().Be(NotificationTemplateKeys.PickupReminder);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }
}
