using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RentACar.API.Services;
using RentACar.API.Contracts.Fleet;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class VehiclePhotoValidationTests
{
    [Theory]
    [InlineData("fake.jpg", "image/jpeg")]
    [InlineData("fake.png", "image/png")]
    [InlineData("fake.webp", "image/webp")]
    [InlineData("fake.svg", "image/svg+xml")]
    public async Task SaveAsync_RejectsRenamedNonImageWithoutWriting(string name, string type)
    {
        var root = Path.Combine(Path.GetTempPath(), "catalogue-invalid-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalVehiclePhotoStorage(root);
        using var stream = new MemoryStream("<script>alert(1)</script>"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = type };
        var act = () => storage.SaveAsync(Guid.NewGuid(), file);
        await act.Should().ThrowAsync<ArgumentException>();
        Directory.Exists(root).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5242881)]
    public async Task SaveAsync_RejectsEmptyAndOversized(long size)
    {
        var root = Path.Combine(Path.GetTempPath(), "catalogue-size-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalVehiclePhotoStorage(root);
        using var stream = new MemoryStream();
        var file = new FormFile(stream, 0, size, "file", "test.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var act = () => storage.SaveAsync(Guid.NewGuid(), file);
        await act.Should().ThrowAsync<ArgumentException>();
        Directory.Exists(root).Should().BeFalse();
    }

    [Fact]
    public void CatalogueValidation_AllowsUnknownsAndRejectsUnverifiedOrInvalidFields()
    {
        VehicleCatalogueValidation.Validate(null, null, null, null, null, null, null, null, null).Should().BeNull();
        VehicleCatalogueValidation.Validate("manual", "diesel", 4, 0, "sedan", 4, "1.5", 100, ["bluetooth"]).Should().BeNull();
        VehicleCatalogueValidation.Validate("magic", null, null, null, null, null, null, null, null).Should().NotBeNull();
        VehicleCatalogueValidation.Validate(null, null, 0, null, null, null, null, null, null).Should().NotBeNull();
        VehicleCatalogueValidation.Validate(null, null, null, null, null, null, null, null, ["unlimitedMileage"]).Should().NotBeNull();
    }
}
