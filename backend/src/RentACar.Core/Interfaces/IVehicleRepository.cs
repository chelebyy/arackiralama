using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IVehicleRepository
{
    Task<IReadOnlyList<Vehicle>> ListAsync(CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsPlateAvailableAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default);
    Task<bool> VehicleGroupExistsAsync(Guid vehicleGroupId, CancellationToken cancellationToken = default);
    Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    void Remove(Vehicle vehicle);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
