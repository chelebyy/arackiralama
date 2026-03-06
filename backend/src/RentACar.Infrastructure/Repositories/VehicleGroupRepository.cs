using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class VehicleGroupRepository(IApplicationDbContext dbContext) : IVehicleGroupRepository
{
    public async Task<IReadOnlyList<VehicleGroup>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.VehicleGroups
            .AsNoTracking()
            .OrderBy(group => group.NameTr)
            .ToListAsync(cancellationToken);
    }

    public Task<VehicleGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.VehicleGroups
            .FirstOrDefaultAsync(group => group.Id == id, cancellationToken);
    }

    public Task AddAsync(VehicleGroup vehicleGroup, CancellationToken cancellationToken = default)
    {
        return dbContext.VehicleGroups.AddAsync(vehicleGroup, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
