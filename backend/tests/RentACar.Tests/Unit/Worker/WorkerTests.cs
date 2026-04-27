using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RentACar.Core.Entities;
using RentACar.Core.Constants;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using RentACar.Worker;
using Xunit;

namespace RentACar.Tests.Unit.Worker;

public sealed class WorkerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public WorkerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task ProcessNotificationJobsAsync_WhenJobsProcessed_LogsInformation()
    {
        var processorMock = new Mock<INotificationBackgroundJobProcessor>();
        processorMock.Setup(p => p.ProcessPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(processorMock: processorMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "ProcessNotificationJobsAsync", CancellationToken.None);

        processorMock.Verify(p => p.ProcessPendingAsync(It.IsAny<CancellationToken>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processed 3 notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNotificationJobsAsync_WhenNoJobs_DoesNotLogInformation()
    {
        var processorMock = new Mock<INotificationBackgroundJobProcessor>();
        processorMock.Setup(p => p.ProcessPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(processorMock: processorMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "ProcessNotificationJobsAsync", CancellationToken.None);

        processorMock.Verify(p => p.ProcessPendingAsync(It.IsAny<CancellationToken>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueExpiredHoldJobsAsync_WhenExpiredHoldsExist_EnqueuesJobs()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var expiredIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var holdServiceMock = new Mock<IReservationHoldService>();
        holdServiceMock.Setup(h => h.GetExpiredHoldsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expiredIds);

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, holdServiceMock: holdServiceMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "EnqueueExpiredHoldJobsAsync", CancellationToken.None);

        dbContext.BackgroundJobs.Should().HaveCount(2);
        dbContext.BackgroundJobs.All(j => j.Type == BackgroundJobTypes.ReservationHoldReleaseExpired).Should().BeTrue();
        dbContext.BackgroundJobs.All(j => j.Status == BackgroundJobStatus.Pending).Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueExpiredHoldJobsAsync_WhenAlreadyEnqueued_SkipsDuplicate()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var reservationId = Guid.NewGuid();
        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
            Status = BackgroundJobStatus.Pending,
            Payload = JsonSerializer.Serialize(new { ReservationId = reservationId }),
            ScheduledAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var holdServiceMock = new Mock<IReservationHoldService>();
        holdServiceMock.Setup(h => h.GetExpiredHoldsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Guid> { reservationId });

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, holdServiceMock: holdServiceMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "EnqueueExpiredHoldJobsAsync", CancellationToken.None);

        dbContext.BackgroundJobs.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessExpiredHoldJobsAsync_WhenPendingJobsExist_ProcessesAndCompletes()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var reservation = new Reservation
        {
            Status = ReservationStatus.Hold,
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            TotalAmount = 1000m
        };
        dbContext.Reservations.Add(reservation);
        var job = new BackgroundJob
        {
            Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
            Status = BackgroundJobStatus.Pending,
            Payload = JsonSerializer.Serialize(new { ReservationId = reservation.Id }),
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync();

        var holdServiceMock = new Mock<IReservationHoldService>();
        var reservationRepoMock = new Mock<IReservationRepository>();
        reservationRepoMock.Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reservation);

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, holdServiceMock: holdServiceMock.Object, reservationRepoMock: reservationRepoMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "ProcessExpiredHoldJobsAsync", CancellationToken.None);

        job.Status.Should().Be(BackgroundJobStatus.Completed);
        reservation.Status.Should().Be(ReservationStatus.Expired);
        holdServiceMock.Verify(h => h.ReleaseHoldAsync(reservation.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredHoldJobsAsync_WhenReservationNotDraftOrHold_SkipsStatusChange()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var reservation = new Reservation
        {
            Status = ReservationStatus.Active,
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            TotalAmount = 1000m
        };
        dbContext.Reservations.Add(reservation);
        var job = new BackgroundJob
        {
            Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
            Status = BackgroundJobStatus.Pending,
            Payload = JsonSerializer.Serialize(new { ReservationId = reservation.Id }),
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync();

        var holdServiceMock = new Mock<IReservationHoldService>();
        var reservationRepoMock = new Mock<IReservationRepository>();
        reservationRepoMock.Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reservation);

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, holdServiceMock: holdServiceMock.Object, reservationRepoMock: reservationRepoMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "ProcessExpiredHoldJobsAsync", CancellationToken.None);

        job.Status.Should().Be(BackgroundJobStatus.Completed);
        reservation.Status.Should().Be(ReservationStatus.Active);
        holdServiceMock.Verify(h => h.ReleaseHoldAsync(reservation.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredHoldJobsAsync_WhenExceptionOccurs_RetriesThenFails()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var reservationId = Guid.NewGuid();
        var job = new BackgroundJob
        {
            Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
            Status = BackgroundJobStatus.Pending,
            Payload = JsonSerializer.Serialize(new { ReservationId = reservationId }),
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync();

        var holdServiceMock = new Mock<IReservationHoldService>();
        holdServiceMock.Setup(h => h.ReleaseHoldAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis error"));

        var reservationRepoMock = new Mock<IReservationRepository>();
        reservationRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => dbContext.Reservations.Find(id));

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, holdServiceMock: holdServiceMock.Object, reservationRepoMock: reservationRepoMock.Object, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        for (int i = 0; i < 3; i++)
        {
            await InvokePrivateAsync(worker, "ProcessExpiredHoldJobsAsync", CancellationToken.None);
            if (i < 2)
            {
                job.ScheduledAt = DateTime.UtcNow.AddMinutes(-1);
                await dbContext.SaveChangesAsync();
            }
        }

        job.Status.Should().Be(BackgroundJobStatus.Failed);
        job.RetryCount.Should().Be(3);
        job.LastError.Should().Contain("Redis error");
    }

    [Fact]
    public async Task EnsureDailyBackupJobScheduledAsync_WhenEnabledAndNoPendingJob_AddsJob()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, logger: loggerMock.Object);

        var options = new DailyBackupOptions { Enabled = true, ScheduleUtcHour = 2, ScheduleUtcMinute = 0 };
        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(options), loggerMock.Object);

        await InvokePrivateAsync(worker, "EnsureDailyBackupJobScheduledAsync", CancellationToken.None);

        dbContext.BackgroundJobs.Should().ContainSingle();
        dbContext.BackgroundJobs.Single().Type.Should().Be(BackgroundJobTypes.DailyBackupRun);
    }

    [Fact]
    public async Task EnsureDailyBackupJobScheduledAsync_WhenDisabled_DoesNotAddJob()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, logger: loggerMock.Object);

        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(new DailyBackupOptions { Enabled = false }), loggerMock.Object);

        await InvokePrivateAsync(worker, "EnsureDailyBackupJobScheduledAsync", CancellationToken.None);

        dbContext.BackgroundJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureDailyBackupJobScheduledAsync_WhenPendingJobExists_DoesNotAddDuplicate()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.DailyBackupRun,
            Status = BackgroundJobStatus.Pending,
            Payload = "{}",
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<RentACar.Worker.Worker>>();
        var serviceProvider = CreateServiceProvider(dbContext, logger: loggerMock.Object);

        var options = new DailyBackupOptions { Enabled = true, ScheduleUtcHour = 2, ScheduleUtcMinute = 0 };
        var worker = new RentACar.Worker.Worker(serviceProvider, Options.Create(options), loggerMock.Object);

        await InvokePrivateAsync(worker, "EnsureDailyBackupJobScheduledAsync", CancellationToken.None);

        dbContext.BackgroundJobs.Should().ContainSingle();
    }

    private static IServiceProvider CreateServiceProvider(
        IApplicationDbContext? dbContext = null,
        INotificationBackgroundJobProcessor? processorMock = null,
        IReservationHoldService? holdServiceMock = null,
        IReservationRepository? reservationRepoMock = null,
        ILogger<RentACar.Worker.Worker>? logger = null)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();

        if (processorMock != null)
        {
            serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationBackgroundJobProcessor))).Returns(processorMock);
        }

        if (dbContext != null)
        {
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IApplicationDbContext))).Returns(dbContext);
        }

        if (holdServiceMock != null)
        {
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IReservationHoldService))).Returns(holdServiceMock);
        }

        if (reservationRepoMock != null)
        {
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IReservationRepository))).Returns(reservationRepoMock);
        }

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        serviceProviderMock.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);

        return serviceProviderMock.Object;
    }

    private static async Task InvokePrivateAsync(RentACar.Worker.Worker worker, string methodName, CancellationToken cancellationToken)
    {
        var method = typeof(RentACar.Worker.Worker).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"Method {methodName} should exist on Worker");

        var task = (Task)method!.Invoke(worker, new object[] { cancellationToken })!;
        await task;
    }
}
