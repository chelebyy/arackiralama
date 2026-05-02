using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class NotificationBackgroundJobProcessorTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACar.Infrastructure.Data.RentACarDbContext _dbContext;

    public NotificationBackgroundJobProcessorTests()
    {
        _dbContext = _dbFactory.CreateContext();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenEmailJobExists_CompletesJob()
    {
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = NotificationQueueService.SendEmailJobType,
            Payload = System.Text.Json.JsonSerializer.Serialize(new QueuedEmailNotificationRequest
            {
                ToEmail = "customer@example.com",
                TemplateKey = NotificationTemplateKeys.PaymentReceived,
                Locale = "tr-TR",
                Variables = new Dictionary<string, string>
                {
                    ["PublicCode"] = "RSV-001"
                }
            }),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _dbContext.SaveChangesAsync();

        var emailProvider = new FakeEmailProvider(success: true);
        var sut = new NotificationBackgroundJobProcessor(
            _dbContext,
            new NotificationTemplateService(),
            emailProvider,
            new FakeSmsProvider(success: true),
            NullLogger<NotificationBackgroundJobProcessor>.Instance);

        var processedCount = await sut.ProcessPendingAsync();

        processedCount.Should().Be(1);
        _dbContext.BackgroundJobs.Single().Status.Should().Be(BackgroundJobStatus.Completed);
        emailProvider.LastRequest.Should().NotBeNull();
        emailProvider.LastRequest!.Subject.Should().Be("Odeme alindi");
        emailProvider.LastRequest.PlainTextBody.Should().Contain("RSV-001");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenSmsJobFails_KeepsJobPendingForRetry()
    {
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = NotificationQueueService.SendSmsJobType,
            Payload = System.Text.Json.JsonSerializer.Serialize(new QueuedSmsNotificationRequest
            {
                ToPhoneNumber = "+905551112233",
                TemplateKey = NotificationTemplateKeys.ReservationCancelled,
                Locale = "tr-TR",
                Variables = new Dictionary<string, string>
                {
                    ["PublicCode"] = "RSV-001"
                }
            }),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _dbContext.SaveChangesAsync();

        var smsProvider = new FakeSmsProvider(success: false);
        var sut = new NotificationBackgroundJobProcessor(
            _dbContext,
            new NotificationTemplateService(),
            new FakeEmailProvider(success: true),
            smsProvider,
            NullLogger<NotificationBackgroundJobProcessor>.Instance);

        var processedCount = await sut.ProcessPendingAsync();

        processedCount.Should().Be(0);
        var job = _dbContext.BackgroundJobs.Single();
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.RetryCount.Should().Be(1);
        job.LastError.Should().Be("SMS failed");
        smsProvider.LastRequest.Should().NotBeNull();
        smsProvider.LastRequest!.Body.Should().Contain("RSV-001");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenBatchSizeConfigured_ProcessesOnlyConfiguredCount()
    {
        _dbContext.BackgroundJobs.AddRange(
            new BackgroundJob
            {
                Type = NotificationQueueService.SendEmailJobType,
                Payload = System.Text.Json.JsonSerializer.Serialize(new QueuedEmailNotificationRequest
                {
                    ToEmail = "first@example.com",
                    TemplateKey = NotificationTemplateKeys.PaymentReceived,
                    Locale = "tr-TR",
                    Variables = new Dictionary<string, string> { ["PublicCode"] = "RSV-001" }
                }),
                Status = BackgroundJobStatus.Pending,
                ScheduledAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new BackgroundJob
            {
                Type = NotificationQueueService.SendEmailJobType,
                Payload = System.Text.Json.JsonSerializer.Serialize(new QueuedEmailNotificationRequest
                {
                    ToEmail = "second@example.com",
                    TemplateKey = NotificationTemplateKeys.PaymentReceived,
                    Locale = "tr-TR",
                    Variables = new Dictionary<string, string> { ["PublicCode"] = "RSV-002" }
                }),
                Status = BackgroundJobStatus.Pending,
                ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await _dbContext.SaveChangesAsync();

        var emailProvider = new FakeEmailProvider(success: true);
        var sut = new NotificationBackgroundJobProcessor(
            _dbContext,
            new NotificationTemplateService(),
            emailProvider,
            new FakeSmsProvider(success: true),
            NullLogger<NotificationBackgroundJobProcessor>.Instance,
            Options.Create(new BackgroundJobProcessorOptions { BatchSize = 1 }));

        var processedCount = await sut.ProcessPendingAsync();

        processedCount.Should().Be(1);
        _dbContext.BackgroundJobs.Count(x => x.Status == BackgroundJobStatus.Completed).Should().Be(1);
        _dbContext.BackgroundJobs.Count(x => x.Status == BackgroundJobStatus.Pending).Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenJobScheduledInFuture_SkipsProcessing()
    {
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = NotificationQueueService.SendEmailJobType,
            Payload = System.Text.Json.JsonSerializer.Serialize(new QueuedEmailNotificationRequest
            {
                ToEmail = "future@example.com",
                TemplateKey = NotificationTemplateKeys.PaymentReceived,
                Locale = "tr-TR",
                Variables = new Dictionary<string, string>
                {
                    ["PublicCode"] = "RSV-FUTURE"
                }
            }),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        });
        await _dbContext.SaveChangesAsync();

        var emailProvider = new FakeEmailProvider(success: true);
        var sut = new NotificationBackgroundJobProcessor(
            _dbContext,
            new NotificationTemplateService(),
            emailProvider,
            new FakeSmsProvider(success: true),
            NullLogger<NotificationBackgroundJobProcessor>.Instance);

        var processedCount = await sut.ProcessPendingAsync();

        processedCount.Should().Be(0);
        emailProvider.LastRequest.Should().BeNull();
        _dbContext.BackgroundJobs.Single().Status.Should().Be(BackgroundJobStatus.Pending);
    }

    private sealed class FakeEmailProvider(bool success) : IEmailProvider
    {
        public EmailMessageRequest? LastRequest { get; private set; }

        public Task<EmailSendResult> SendAsync(EmailMessageRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new EmailSendResult
            {
                Success = success,
                Provider = "FakeEmail",
                FailureCode = success ? null : "FAILED",
                FailureMessage = success ? null : "Email failed"
            });
        }
    }

    private sealed class FakeSmsProvider(bool success) : ISmsProvider
    {
        public SmsMessageRequest? LastRequest { get; private set; }

        public Task<SmsSendResult> SendAsync(SmsMessageRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SmsSendResult
            {
                Success = success,
                Provider = "FakeSms",
                FailureCode = success ? null : "FAILED",
                FailureMessage = success ? null : "SMS failed"
            });
        }
    }
}
