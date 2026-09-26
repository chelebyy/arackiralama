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
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Gecersiz gorsel formati.");
        }

        if (file.Length is <= 0 or > 5 * 1024 * 1024)
            throw new ArgumentException("Gorsel dosya boyutu gecersiz (en fazla 5MB).");
        await using var input = file.OpenReadStream();
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + read > 5 * 1024 * 1024)
                throw new ArgumentException("Gorsel dosya boyutu gecersiz (en fazla 5MB).");
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var bytes = content.ToArray();
        var valid = extension switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 &&
                bytes[2] == 0xff && bytes[^2] == 0xff && bytes[^1] == 0xd9,
            ".png" => bytes.Length >= 45 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10}) &&
                bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) && bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8),
            ".webp" => bytes.Length >= 30 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) == bytes.Length - 8,
            _ => false
        };
        var expectedContentType = extension is ".jpg" or ".jpeg" ? "image/jpeg" : $"image/{extension[1..]}";
        if (!valid || !string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Dosya icerigi gecerli bir JPG, PNG veya WEBP gorseli degil.");
        Directory.CreateDirectory(_storageRoot);

        var fileName = $"{vehicleId:N}-{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(_storageRoot, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, cancellationToken);

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
