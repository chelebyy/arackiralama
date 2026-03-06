using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IVehicleGroupRepository
{
    Task<IReadOnlyList<VehicleGroup>> ListAsync(CancellationToken cancellationToken = default);
    Task<VehicleGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(VehicleGroup vehicleGroup, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
