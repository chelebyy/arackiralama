using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class ReservationRepository(IApplicationDbContext dbContext)
    : Repository<Reservation>(dbContext, dbContext.Reservations), IReservationRepository
{
    protected override IQueryable<Reservation> BuildListQuery()
    {
        return Entities
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .OrderByDescending(r => r.CreatedAt);
    }

    public Task<Reservation?> GetByPublicCodeAsync(string publicCode, CancellationToken cancellationToken = default)
    {
        return Entities
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.PublicCode == publicCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var results = await Entities
            .AsNoTracking()
            .Include(r => r.Vehicle)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<Reservation>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var results = await Entities
            .AsNoTracking()
            .Include(r => r.Customer)
            .Where(r => r.VehicleId == vehicleId)
            .OrderBy(r => r.PickupDateTime)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<Reservation>> GetActiveReservationsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            ReservationStatus.Hold,
            ReservationStatus.PendingPayment,
            ReservationStatus.Paid,
            ReservationStatus.Active
        };

        var results = await Entities
            .AsNoTracking()
            .Include(r => r.Customer)
            .Where(r => r.VehicleId == vehicleId && activeStatuses.Contains(r.Status))
            .OrderBy(r => r.PickupDateTime)
            .ToListAsync(cancellationToken);

        return results;
    }

    public Task<bool> HasOverlappingReservationsAsync(
        Guid vehicleId,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        Guid? excludeReservationId = null,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            ReservationStatus.Hold,
            ReservationStatus.PendingPayment,
            ReservationStatus.Paid,
            ReservationStatus.Active
        };

        var query = Entities
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .Where(r => activeStatuses.Contains(r.Status))
            .Where(r => r.PickupDateTime < returnDateTime && r.ReturnDateTime > pickupDateTime);

        if (excludeReservationId.HasValue)
        {
            query = query.Where(r => r.Id != excludeReservationId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> GetReservationsByStatusAsync(
        ReservationStatus status,
        CancellationToken cancellationToken = default)
    {
        var results = await Entities
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .Where(r => r.Status == status)
            .OrderBy(r => r.PickupDateTime)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<Reservation>> GetExpiredReservationsAsync(
        DateTime beforeDate,
        CancellationToken cancellationToken = default)
    {
        var results = await Entities
            .AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Hold && r.CreatedAt < beforeDate)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<Reservation>> SearchReservationsAsync(
        Guid? customerId = null,
        Guid? vehicleId = null,
        ReservationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = Entities.AsNoTracking();

        if (customerId.HasValue)
        {
            query = query.Where(r => r.CustomerId == customerId.Value);
        }

        if (vehicleId.HasValue)
        {
            query = query.Where(r => r.VehicleId == vehicleId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.PickupDateTime >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.ReturnDateTime <= toDate.Value);
        }

        var results = await query
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<(IReadOnlyList<Reservation> Items, int TotalCount)> GetByCustomerIdPaginatedAsync(
        Guid customerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = Entities
            .AsNoTracking()
            .Include(r => r.Vehicle)
            .Where(r => r.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
