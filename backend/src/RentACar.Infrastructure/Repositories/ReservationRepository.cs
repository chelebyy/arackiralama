using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class ReservationRepository(IApplicationDbContext dbContext)
    : Repository<Reservation>(dbContext, dbContext.Reservations), IReservationRepository
{
    private static IQueryable<Reservation> IncludeReservationDetails(IQueryable<Reservation> query)
    {
        return query
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
                .ThenInclude(v => v!.Group)
            .Include(r => r.Vehicle)
                .ThenInclude(v => v!.Office)
            .Include(r => r.PickupOffice)
            .Include(r => r.ReturnOffice)
            .Include(r => r.SelectedExtras);
    }

    protected override IQueryable<Reservation> BuildListQuery()
    {
        return IncludeReservationDetails(Entities.AsNoTracking())
            .OrderByDescending(r => r.CreatedAt);
    }

    public override Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return IncludeReservationDetails(Entities)
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }

    public Task<Reservation?> GetByPublicCodeAsync(string publicCode, CancellationToken cancellationToken = default)
    {
        return IncludeReservationDetails(Entities.AsNoTracking())
            .FirstOrDefaultAsync(r => r.PublicCode == publicCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var results = await IncludeReservationDetails(Entities.AsNoTracking())
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<Reservation>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var results = await IncludeReservationDetails(Entities.AsNoTracking())
            .Where(r => r.VehicleId == vehicleId)
            .OrderBy(r => r.PickupDateTime)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<Reservation>> GetActiveReservationsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var results = await Entities
            .AsNoTracking()
            .Include(r => r.Customer)
            .Where(r => r.VehicleId == vehicleId && ReservationStatusGroups.StockBlocking.Contains(r.Status))
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
        var query = Entities
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .Where(r => ReservationStatusGroups.StockBlocking.Contains(r.Status))
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
        var results = await IncludeReservationDetails(Entities.AsNoTracking())
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
        string? searchTerm = null,
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

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearchTerm = searchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.PublicCode.ToLower().Contains(normalizedSearchTerm) ||
                (r.Customer != null && (
                    r.Customer.FullName.ToLower().Contains(normalizedSearchTerm) ||
                    r.Customer.Email.ToLower().Contains(normalizedSearchTerm) ||
                    r.Customer.Phone.ToLower().Contains(normalizedSearchTerm))) ||
                (r.Vehicle != null && (
                    r.Vehicle.Plate.ToLower().Contains(normalizedSearchTerm) ||
                    r.Vehicle.Brand.ToLower().Contains(normalizedSearchTerm) ||
                    r.Vehicle.Model.ToLower().Contains(normalizedSearchTerm))));
        }

        var results = await IncludeReservationDetails(query)
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
        var query = IncludeReservationDetails(Entities.AsNoTracking())
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
