namespace RentACar.API.Services;

public sealed class LocalVehiclePhotoStorage : IVehiclePhotoStorage
{
    private const string PublicPrefix = "/uploads/vehicles/";
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private readonly string _storageRoot;

    public LocalVehiclePhotoStorage(IWebHostEnvironment environment)
        : this(string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath)
    {
    }

    public LocalVehiclePhotoStorage(string webRootPath)
    {
        _storageRoot = Path.Combine(webRootPath, "uploads", "vehicles");
    }

    public async Task<string> SaveAsync(Guid vehicleId, IFormFile file, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storageRoot);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported vehicle photo extension.");
        }

        var fileName = $"{vehicleId:N}-{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(_storageRoot, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);

        return $"{PublicPrefix}{fileName}";
    }

    public Task DeleteAsync(string? photoUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(photoUrl) ||
            !photoUrl.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var fileName = photoUrl[PublicPrefix.Length..];
        if (fileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return Task.CompletedTask;
        }

        var filePath = Path.Combine(_storageRoot, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
