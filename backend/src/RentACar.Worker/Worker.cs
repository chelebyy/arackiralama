using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RentACar.Core.Constants;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.Worker;

public sealed class Worker(
    IServiceProvider serviceProvider,
    IOptions<DailyBackupOptions> dailyBackupOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int HoldReleaseRetryLimit = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationJobsAsync(stoppingToken);
                await EnqueueExpiredHoldJobsAsync(stoppingToken);
                await ProcessExpiredHoldJobsAsync(stoppingToken);
                await EnsureDailyBackupJobScheduledAsync(stoppingToken);
                await ProcessDailyBackupJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process background jobs");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessNotificationJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<INotificationBackgroundJobProcessor>();
        var processedCount = await processor.ProcessPendingAsync(cancellationToken);

        if (processedCount > 0)
        {
            logger.LogInformation("Processed {ProcessedCount} notification background jobs", processedCount);
        }
    }

    private async Task EnqueueExpiredHoldJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var expiredReservationIds = await holdService.GetExpiredHoldsAsync(cancellationToken);
        if (expiredReservationIds.Count == 0)
        {
            return;
        }

        var enqueuedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var reservationId in expiredReservationIds)
        {
            var payload = JsonSerializer.Serialize(new ReleaseExpiredHoldPayload(reservationId));
            var hasRunnableJob = await dbContext.BackgroundJobs
                .AsNoTracking()
                .AnyAsync(
                    x => x.Type == BackgroundJobTypes.ReservationHoldReleaseExpired
                        && x.Payload == payload
                        && (x.Status == BackgroundJobStatus.Pending || x.Status == BackgroundJobStatus.Processing),
                    cancellationToken);

            if (hasRunnableJob)
            {
                continue;
            }

            var job = new BackgroundJob
            {
                Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
                Payload = payload,
                Status = BackgroundJobStatus.Pending,
                ScheduledAt = now
            };

            dbContext.BackgroundJobs.Add(job);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                enqueuedCount++;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                GetDbContext(dbContext).Entry(job).State = EntityState.Detached;
                logger.LogInformation(
                    ex,
                    "Skipped concurrent duplicate expired hold job for reservation {ReservationId}",
                    reservationId);
            }
        }

        if (enqueuedCount > 0)
        {
            logger.LogInformation("Enqueued {EnqueuedCount} expired hold release jobs", enqueuedCount);
        }
    }

    private async Task ProcessExpiredHoldJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var jobIds = await dbContext.BackgroundJobs
            .Where(x =>
                x.Type == BackgroundJobTypes.ReservationHoldReleaseExpired
                && x.Status == BackgroundJobStatus.Pending
                && x.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(x => x.ScheduledAt)
            .Select(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (jobIds.Count == 0)
        {
            return;
        }

        var processedCount = 0;
        foreach (var jobId in jobIds)
        {
            if (!await TryClaimExpiredHoldJobAsync(dbContext, jobId, cancellationToken))
            {
                continue;
            }

            var job = await dbContext.BackgroundJobs.SingleAsync(x => x.Id == jobId, cancellationToken);

            try
            {
                var payload = JsonSerializer.Deserialize<ReleaseExpiredHoldPayload>(job.Payload)
                    ?? throw new InvalidOperationException("ReleaseExpiredHold payload çözümlenemedi.");

                var released = await holdService.ReleaseHoldAsync(payload.ReservationId, cancellationToken);
                if (!released)
                {
                    throw new InvalidOperationException($"Failed to release hold for reservation {payload.ReservationId}");
                }

                var reservation = await reservationRepository.GetByIdAsync(payload.ReservationId, cancellationToken);
                if (reservation != null && reservation.Status is ReservationStatus.Hold or ReservationStatus.Draft)
                {
                    reservation.Status = ReservationStatus.Expired;
                    reservation.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                job.Status = BackgroundJobStatus.Completed;
                job.LastError = null;
                job.FailedAt = null;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                processedCount++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex,
                    "Concurrency conflict while processing expired hold job {BackgroundJobId}",
                    job.Id);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.LastError = ex.Message;
                job.UpdatedAt = DateTime.UtcNow;
                if (job.RetryCount >= HoldReleaseRetryLimit)
                {
                    job.Status = BackgroundJobStatus.Failed;
                    job.FailedAt = DateTime.UtcNow;
                }
                else
                {
                    job.Status = BackgroundJobStatus.Pending;
                    job.ScheduledAt = DateTime.UtcNow.AddSeconds(15 * job.RetryCount);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(ex, "Release expired hold job {BackgroundJobId} failed", job.Id);
            }
        }

        if (processedCount > 0)
        {
            logger.LogInformation("Processed {ProcessedCount} expired hold release jobs", processedCount);
        }
    }

    private async Task EnsureDailyBackupJobScheduledAsync(CancellationToken cancellationToken)
    {
        if (!dailyBackupOptions.Value.Enabled)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;
        var nextRun = new DateTime(
            now.Year,
            now.Month,
            now.Day,
            Math.Clamp(dailyBackupOptions.Value.ScheduleUtcHour, 0, 23),
            Math.Clamp(dailyBackupOptions.Value.ScheduleUtcMinute, 0, 59),
            0,
            DateTimeKind.Utc);

        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        var hasPendingOrProcessing = await dbContext.BackgroundJobs
            .AsNoTracking()
            .AnyAsync(
                x => x.Type == BackgroundJobTypes.DailyBackupRun
                    && (x.Status == BackgroundJobStatus.Pending || x.Status == BackgroundJobStatus.Processing),
                cancellationToken);

        if (hasPendingOrProcessing)
        {
            return;
        }

        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.DailyBackupRun,
            Payload = JsonSerializer.Serialize(new DailyBackupPayload(nextRun)),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = nextRun
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Scheduled daily backup job for {ScheduledAtUtc}", nextRun);
    }

    private async Task ProcessDailyBackupJobsAsync(CancellationToken cancellationToken)
    {
        if (!dailyBackupOptions.Value.Enabled)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var jobs = await dbContext.BackgroundJobs
            .Where(x =>
                x.Type == BackgroundJobTypes.DailyBackupRun
                && x.Status == BackgroundJobStatus.Pending
                && x.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(x => x.ScheduledAt)
            .Take(1)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            try
            {
                job.Status = BackgroundJobStatus.Processing;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                await ExecuteDailyBackupCommandAsync(cancellationToken);

                job.Status = BackgroundJobStatus.Completed;
                job.LastError = null;
                job.FailedAt = null;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.LastError = ex.Message;
                job.UpdatedAt = DateTime.UtcNow;

                var retryLimit = Math.Max(dailyBackupOptions.Value.RetryLimit, 1);
                if (job.RetryCount >= retryLimit)
                {
                    job.Status = BackgroundJobStatus.Failed;
                    job.FailedAt = DateTime.UtcNow;
                }
                else
                {
                    job.Status = BackgroundJobStatus.Pending;
                    job.ScheduledAt = DateTime.UtcNow.AddMinutes(15 * job.RetryCount);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(ex, "Daily backup job {BackgroundJobId} failed", job.Id);
            }
        }
    }

    private async Task ExecuteDailyBackupCommandAsync(CancellationToken cancellationToken)
    {
        var startInfo = DailyBackupCommandPolicy.CreateStartInfo(dailyBackupOptions.Value);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Daily backup process could not be started.");
        }

        var timeoutSeconds = Math.Max(dailyBackupOptions.Value.TimeoutSeconds, 30);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to kill backup process");
            }

            throw new TimeoutException($"Daily backup process timed out after {timeoutSeconds} seconds.");
        }

        await Task.WhenAll(stdoutTask, stderrTask);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Daily backup command failed with exit code {process.ExitCode}. stderr: {DailyBackupCommandPolicy.SanitizeForLog(stderr)}");
        }

        logger.LogInformation(
            "Daily backup command completed successfully. stdout: {Output}",
            DailyBackupCommandPolicy.SanitizeForLog(stdout));
    }

    private static DbContext GetDbContext(IApplicationDbContext dbContext)
    {
        return dbContext as DbContext
            ?? throw new InvalidOperationException("IApplicationDbContext must also derive from DbContext.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    private static async Task<bool> TryClaimExpiredHoldJobAsync(
        IApplicationDbContext dbContext,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var efDbContext = GetDbContext(dbContext);

        if (!efDbContext.Database.IsRelational())
        {
            var job = await dbContext.BackgroundJobs.SingleOrDefaultAsync(
                x => x.Id == jobId && x.Status == BackgroundJobStatus.Pending,
                cancellationToken);
            if (job is null)
            {
                return false;
            }

            job.Status = BackgroundJobStatus.Processing;
            job.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affectedRows = await dbContext.BackgroundJobs
            .Where(x => x.Id == jobId && x.Status == BackgroundJobStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, BackgroundJobStatus.Processing)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        return affectedRows == 1;
    }
}

public sealed class DailyBackupOptions
{
    public bool Enabled { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> AllowedCommands { get; set; } = [];
    public int ScheduleUtcHour { get; set; } = 2;
    public int ScheduleUtcMinute { get; set; }
    public int TimeoutSeconds { get; set; } = 900;
    public int RetryLimit { get; set; } = 3;
}

internal sealed record ReleaseExpiredHoldPayload(Guid ReservationId);
internal sealed record DailyBackupPayload(DateTime ScheduledForUtc);
