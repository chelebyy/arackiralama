using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class VehicleRepository(IApplicationDbContext dbContext) : IVehicleRepository
{
    public async Task<IReadOnlyList<Vehicle>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(vehicle => vehicle.Plate)
            .ToListAsync(cancellationToken);
    }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Vehicles.FirstOrDefaultAsync(vehicle => vehicle.Id == id, cancellationToken);
    }

    public Task<bool> IsPlateAvailableAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default)
    {
        var normalizedPlate = plate.Trim().ToUpperInvariant();

        return dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => !excludeVehicleId.HasValue || vehicle.Id != excludeVehicleId.Value)
            .AllAsync(vehicle => vehicle.Plate.ToUpper() != normalizedPlate, cancellationToken);
    }

    public Task<bool> VehicleGroupExistsAsync(Guid vehicleGroupId, CancellationToken cancellationToken = default)
    {
        return dbContext.VehicleGroups
            .AsNoTracking()
            .AnyAsync(group => group.Id == vehicleGroupId, cancellationToken);
    }

    public Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default)
    {
        return dbContext.Offices
            .AsNoTracking()
            .AnyAsync(office => office.Id == officeId, cancellationToken);
    }

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        return dbContext.Vehicles.AddAsync(vehicle, cancellationToken).AsTask();
    }

    public void Remove(Vehicle vehicle)
    {
        dbContext.Vehicles.Remove(vehicle);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
