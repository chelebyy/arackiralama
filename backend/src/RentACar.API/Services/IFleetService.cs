using RentACar.API.Contracts.Fleet;
using RentACar.Core.Enums;

namespace RentACar.API.Services;

public interface IFleetService
{
    Task<IReadOnlyList<VehicleGroupDto>> GetVehicleGroupsAsync(CancellationToken cancellationToken = default);
    Task<VehicleGroupDto> CreateVehicleGroupAsync(CreateVehicleGroupRequest request, CancellationToken cancellationToken = default);
    Task<VehicleGroupDto?> UpdateVehicleGroupAsync(Guid id, UpdateVehicleGroupRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableVehicleGroupDto>> SearchAvailableVehicleGroupsAsync(
        Guid officeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        Guid? vehicleGroupId = null,
        CancellationToken cancellationToken = default);
    Task<VehicleDto?> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default);
    Task<VehicleDto?> UpdateVehicleAsync(Guid id, UpdateVehicleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteVehicleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VehicleDto?> UpdateVehicleStatusAsync(Guid id, VehicleStatus status, CancellationToken cancellationToken = default);
    Task<VehicleDto?> TransferVehicleAsync(Guid id, Guid targetOfficeId, VehicleStatus? status, CancellationToken cancellationToken = default);
    Task<VehicleDto?> ScheduleVehicleMaintenanceAsync(
        Guid id,
        DateTime startDateUtc,
        DateTime endDateUtc,
        string? notes,
        CancellationToken cancellationToken = default);
    Task<VehicleDto?> UploadVehiclePhotoAsync(Guid id, IFormFile file, CancellationToken cancellationToken = default);

    Task<VehicleDto?> GetVehicleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsVehiclePlateAvailableAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default);
    Task<bool> VehicleGroupExistsAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfficeDto>> GetOfficesAsync(CancellationToken cancellationToken = default);
    Task<OfficeDto> CreateOfficeAsync(CreateOfficeRequest request, CancellationToken cancellationToken = default);
    Task<OfficeDto?> UpdateOfficeAsync(Guid id, UpdateOfficeRequest request, CancellationToken cancellationToken = default);
}