namespace RentACar.API.Services;

public interface IVehiclePhotoStorage
{
    Task<string> SaveAsync(Guid vehicleId, IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? photoUrl, CancellationToken cancellationToken = default);
}
