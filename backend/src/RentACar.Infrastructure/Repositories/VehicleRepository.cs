using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class VehicleRepository(IApplicationDbContext dbContext) : Repository<Vehicle>(dbContext, dbContext.Vehicles), IVehicleRepository
{
    protected override IQueryable<Vehicle> BuildListQuery()
    {
        return Entities
            .AsNoTracking()
            .OrderBy(vehicle => vehicle.Plate);
    }

    public Task<bool> IsPlateAvailableAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default)
    {
        var normalizedPlate = plate.Trim().ToUpperInvariant();

        return Entities
            .AsNoTracking()
            .Where(vehicle => !excludeVehicleId.HasValue || vehicle.Id != excludeVehicleId.Value)
            .AllAsync(vehicle => vehicle.Plate.ToUpper() != normalizedPlate, cancellationToken);
    }

    public Task<bool> VehicleGroupExistsAsync(Guid vehicleGroupId, CancellationToken cancellationToken = default)
    {
        return DbContext.VehicleGroups
            .AsNoTracking()
            .AnyAsync(group => group.Id == vehicleGroupId, cancellationToken);
    }

    public Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default)
    {
        return DbContext.Offices
            .AsNoTracking()
            .AnyAsync(office => office.Id == officeId, cancellationToken);
    }
}
