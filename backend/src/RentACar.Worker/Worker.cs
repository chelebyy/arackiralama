using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.Worker;

public sealed class Worker(IServiceProvider serviceProvider, ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reservation expiry worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredHoldsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process expired reservation holds");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredHoldsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var expiredReservationIds = await holdService.GetExpiredHoldsAsync(cancellationToken);
        if (expiredReservationIds.Count == 0)
        {
            return;
        }

        var processedCount = 0;

        foreach (var reservationId in expiredReservationIds)
        {
            var reservation = await reservationRepository.GetByIdAsync(reservationId, cancellationToken);
            if (reservation == null)
            {
                await holdService.ReleaseHoldAsync(reservationId, cancellationToken);
                continue;
            }

            if (reservation.Status is not (ReservationStatus.Hold or ReservationStatus.Draft))
            {
                await holdService.ReleaseHoldAsync(reservationId, cancellationToken);
                continue;
            }

            reservation.Status = ReservationStatus.Expired;
            reservation.UpdatedAt = DateTime.UtcNow;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await holdService.ReleaseHoldAsync(reservationId, cancellationToken);
                processedCount++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex,
                    "Concurrency conflict while expiring reservation {ReservationId}",
                    reservationId);
            }
        }

        logger.LogInformation(
            "Processed {ProcessedCount} expired reservations",
            processedCount);
    }
}
