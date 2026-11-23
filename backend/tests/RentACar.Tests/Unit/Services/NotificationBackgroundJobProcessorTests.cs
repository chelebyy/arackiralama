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

    [Theory]
    [InlineData(BackgroundJobStatus.Pending, 0)]
    [InlineData(BackgroundJobStatus.Pending, 1)]
    [InlineData(BackgroundJobStatus.Processing, 0)]
    public async Task ProcessPendingAsync_ExpiredAccessIsCancelledWithoutDecryptingOrSending(BackgroundJobStatus status, int retries)
    {
        var job = new BackgroundJob
        {
            Type = NotificationQueueService.SendEmailJobType, Status = status, RetryCount = retries,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-20), UpdatedAt = DateTime.UtcNow.AddMinutes(-20),
            Payload = System.Text.Json.JsonSerializer.Serialize(new QueuedEmailNotificationRequest
            {
                ToEmail = "guest@example.test", TemplateKey = "guest-reservation-access",
                Variables = new Dictionary<string, string>
                {
                    ["ProtectedCode"] = "cannot-be-decrypted",
                    ["ExpiresAtUtc"] = DateTime.UtcNow.AddMinutes(-10).ToString("O")
                }
            })
        };
        _dbContext.BackgroundJobs.Add(job);
        await _dbContext.SaveChangesAsync();
        var email = new FakeEmailProvider(success: true);
        var processor = new NotificationBackgroundJobProcessor(_dbContext, new NotificationTemplateService(), email,
            new FakeSmsProvider(success: true), NullLogger<NotificationBackgroundJobProcessor>.Instance);
        (await processor.ProcessPendingAsync()).Should().Be(0);
        job.Status.Should().Be(BackgroundJobStatus.Cancelled);
        job.RetryCount.Should().Be(retries);
        email.LastRequest.Should().BeNull();
        (await processor.ProcessPendingAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(NotificationTemplateKeys.PickupReminder, false, false)]
    [InlineData(NotificationTemplateKeys.PickupReminder, false, true)]
    [InlineData(NotificationTemplateKeys.PickupReminder, true, false)]
    [InlineData(NotificationTemplateKeys.PickupReminder, true, true)]
    [InlineData(NotificationTemplateKeys.ReturnReminder, false, false)]
    [InlineData(NotificationTemplateKeys.ReturnReminder, false, true)]
    [InlineData(NotificationTemplateKeys.ReturnReminder, true, false)]
    [InlineData(NotificationTemplateKeys.ReturnReminder, true, true)]
    public async Task ProcessPendingAsync_SupersededReminderIsCancelled(string template, bool sms, bool cancelled)
    {
        var reservation = new Reservation
        {
            PublicCode = "RECOVERED-REMINDER",
            Status = cancelled ? ReservationStatus.Cancelled : ReservationStatus.Confirmed
        };
        _dbContext.Reservations.Add(reservation);
        var job = AddReminder(reservation, template, sms);
        job.Status = BackgroundJobStatus.Processing;
        job.UpdatedAt = DateTime.UtcNow.AddMinutes(-6);
        if (!cancelled)
            _dbContext.ReservationAmendments.Add(new ReservationAmendment
            {
                ReservationId = reservation.Id, AppliedAt = job.CreatedAt.AddMinutes(1)
            });
        await _dbContext.SaveChangesAsync();
        var emailProvider = new FakeEmailProvider(success: true);
        var smsProvider = new FakeSmsProvider(success: true);
        var processor = new NotificationBackgroundJobProcessor(_dbContext, new NotificationTemplateService(),
            emailProvider, smsProvider, NullLogger<NotificationBackgroundJobProcessor>.Instance);

        (await processor.ProcessPendingAsync()).Should().Be(0);
        job.Status.Should().Be(BackgroundJobStatus.Cancelled);
        job.RetryCount.Should().Be(0);
        job.LastError.Should().Be("Reservation reminder is no longer current.");
        emailProvider.LastRequest.Should().BeNull();
        smsProvider.LastRequest.Should().BeNull();
        (await processor.ProcessPendingAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(NotificationTemplateKeys.PickupReminder, false)]
    [InlineData(NotificationTemplateKeys.PickupReminder, true)]
    [InlineData(NotificationTemplateKeys.ReturnReminder, false)]
    [InlineData(NotificationTemplateKeys.ReturnReminder, true)]
    public async Task ProcessPendingAsync_ReplacementReminderSurvivesProviderRetry(string template, bool sms)
    {
        var reservation = new Reservation { PublicCode = "REPLACEMENT-REMINDER", Status = ReservationStatus.Confirmed };
        _dbContext.Reservations.Add(reservation);
        var job = AddReminder(reservation, template, sms);
        var scheduledAt = job.ScheduledAt;
        _dbContext.ReservationAmendments.AddRange(
            new ReservationAmendment { ReservationId = reservation.Id, AppliedAt = job.CreatedAt.AddMinutes(-1) },
            new ReservationAmendment { ReservationId = reservation.Id, AppliedAt = null });
        await _dbContext.SaveChangesAsync();
        var failingProcessor = new NotificationBackgroundJobProcessor(_dbContext, new NotificationTemplateService(),
            new FakeEmailProvider(success: false), new FakeSmsProvider(success: false),
            NullLogger<NotificationBackgroundJobProcessor>.Instance);

        (await failingProcessor.ProcessPendingAsync()).Should().Be(0);
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.RetryCount.Should().Be(1);
        job.ScheduledAt.Should().BeAfter(scheduledAt);
        job.ScheduledAt = DateTime.UtcNow.AddSeconds(-1);
        await _dbContext.SaveChangesAsync();
        var emailProvider = new FakeEmailProvider(success: true);
        var smsProvider = new FakeSmsProvider(success: true);
        var retryProcessor = new NotificationBackgroundJobProcessor(_dbContext, new NotificationTemplateService(),
            emailProvider, smsProvider, NullLogger<NotificationBackgroundJobProcessor>.Instance);

        (await retryProcessor.ProcessPendingAsync()).Should().Be(1);
        job.Status.Should().Be(BackgroundJobStatus.Completed);
        job.RetryCount.Should().Be(1);
        job.LastError.Should().BeNull();
        if (sms) smsProvider.LastRequest.Should().NotBeNull();
        else emailProvider.LastRequest.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessPendingAsync_RechecksReservationAfterRenderingReminder(bool sms)
    {
        var reservation = new Reservation { PublicCode = "CANCELLED-DURING-RENDER", Status = ReservationStatus.Confirmed };
        _dbContext.Reservations.Add(reservation);
        var job = AddReminder(reservation, NotificationTemplateKeys.PickupReminder, sms);
        await _dbContext.SaveChangesAsync();
        var templateService = new ChangingNotificationTemplateService(() =>
        {
            reservation.Status = ReservationStatus.Cancelled;
            _dbContext.SaveChanges();
        });
        var emailProvider = new FakeEmailProvider(success: true);
        var smsProvider = new FakeSmsProvider(success: true);
        var processor = new NotificationBackgroundJobProcessor(_dbContext, templateService,
            emailProvider, smsProvider, NullLogger<NotificationBackgroundJobProcessor>.Instance);

        (await processor.ProcessPendingAsync()).Should().Be(0);
        job.Status.Should().Be(BackgroundJobStatus.Cancelled);
        emailProvider.LastRequest.Should().BeNull();
        smsProvider.LastRequest.Should().BeNull();
    }

    private BackgroundJob AddReminder(Reservation reservation, string template, bool sms)
    {
        var variables = new Dictionary<string, string> { ["PublicCode"] = reservation.PublicCode };
        var job = new BackgroundJob
        {
            Type = sms ? NotificationQueueService.SendSmsJobType : NotificationQueueService.SendEmailJobType,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10), ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            Payload = sms
                ? System.Text.Json.JsonSerializer.Serialize(new QueuedSmsNotificationRequest
                {
                    ToPhoneNumber = "+900000000000", TemplateKey = template, Variables = variables
                })
                : System.Text.Json.JsonSerializer.Serialize(new QueuedEmailNotificationRequest
                {
                    ToEmail = "guest@example.test", TemplateKey = template, Variables = variables
                })
        };
        _dbContext.BackgroundJobs.Add(job);
        return job;
    }

    private sealed class ChangingNotificationTemplateService(Action beforeRender) : INotificationTemplateService
    {
        private readonly NotificationTemplateService templates = new();
        public EmailMessageRequest RenderEmail(QueuedEmailNotificationRequest request)
        {
            beforeRender();
            return templates.RenderEmail(request);
        }
        public SmsMessageRequest RenderSms(QueuedSmsNotificationRequest request)
        {
            beforeRender();
            return templates.RenderSms(request);
        }
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

    [Fact]
    public async Task ProcessPendingAsync_WhenSmsJobSucceeds_CompletesJob()
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
                    ["PublicCode"] = "RSV-SMS-SUCCESS"
                }
            }),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _dbContext.SaveChangesAsync();

        var smsProvider = new FakeSmsProvider(success: true);
        var sut = new NotificationBackgroundJobProcessor(
            _dbContext,
            new NotificationTemplateService(),
            new FakeEmailProvider(success: true),
            smsProvider,
            NullLogger<NotificationBackgroundJobProcessor>.Instance);

        var processedCount = await sut.ProcessPendingAsync();

        processedCount.Should().Be(1);
        var job = _dbContext.BackgroundJobs.Single();
        job.Status.Should().Be(BackgroundJobStatus.Completed);
        job.LastError.Should().BeNull();
        job.FailedAt.Should().BeNull();
        smsProvider.LastRequest.Should().NotBeNull();
        smsProvider.LastRequest!.Body.Should().Contain("RSV-SMS-SUCCESS");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenRetryLimitReached_MarksJobFailed()
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
                    ["PublicCode"] = "RSV-SMS-FAILED"
                }
            }),
            Status = BackgroundJobStatus.Pending,
            RetryCount = 2,
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
        job.Status.Should().Be(BackgroundJobStatus.Failed);
        job.RetryCount.Should().Be(3);
        job.FailedAt.Should().NotBeNull();
        job.LastError.Should().Be("SMS failed");
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
