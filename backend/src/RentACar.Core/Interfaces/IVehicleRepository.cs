using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<bool> IsPlateAvailableAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default);
    Task<bool> VehicleGroupExistsAsync(Guid vehicleGroupId, CancellationToken cancellationToken = default);
    Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default);
}
