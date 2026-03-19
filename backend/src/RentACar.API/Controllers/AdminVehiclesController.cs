using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/vehicles")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminVehiclesController(
    IFleetService fleetService,
    IAuditLogService auditLogService) : BaseApiController
{
    private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;
    private const string EntityType = "Vehicle";

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var vehicles = await fleetService.GetVehiclesAsync(cancellationToken);
        return OkResponse(vehicles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateVehicleInput(request.Plate, request.Brand, request.Model, request.Year, request.Color);
        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        if (!await fleetService.VehicleGroupExistsAsync(request.GroupId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir arac grubu secilmelidir.");
        }

        if (!await fleetService.OfficeExistsAsync(request.OfficeId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir ofis secilmelidir.");
        }

        if (!await fleetService.IsVehiclePlateAvailableAsync(request.Plate, null, cancellationToken))
        {
            return BadRequestResponse("Bu plaka baska bir arac tarafindan kullaniliyor.");
        }

        var createdVehicle = await fleetService.CreateVehicleAsync(request, cancellationToken);

        await auditLogService.LogAsync(
            "Create",
            EntityType,
            createdVehicle!.Id.ToString(),
            GetCurrentUserId(),
            null,
            System.Text.Json.JsonSerializer.Serialize(request),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(createdVehicle, "Arac olusturuldu.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateVehicleInput(request.Plate, request.Brand, request.Model, request.Year, request.Color);
        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        if (!await fleetService.VehicleGroupExistsAsync(request.GroupId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir arac grubu secilmelidir.");
        }

        if (!await fleetService.OfficeExistsAsync(request.OfficeId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir ofis secilmelidir.");
        }

        if (!await fleetService.IsVehiclePlateAvailableAsync(request.Plate, id, cancellationToken))
        {
            return BadRequestResponse("Bu plaka baska bir arac tarafindan kullaniliyor.");
        }

        var existingVehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);
        var updatedVehicle = await fleetService.UpdateVehicleAsync(id, request, cancellationToken);
        if (updatedVehicle is null)
        {
            return NotFound(ApiResponse<object>.Fail("Arac bulunamadi."));
        }

        await auditLogService.LogAsync(
            "Update",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            existingVehicle != null ? System.Text.Json.JsonSerializer.Serialize(existingVehicle) : null,
            System.Text.Json.JsonSerializer.Serialize(request),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(updatedVehicle, "Arac guncellendi.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var existingVehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);

        var deleted = await fleetService.DeleteVehicleAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.Fail("Arac bulunamadi."));
        }

        await auditLogService.LogAsync(
            "Delete",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            existingVehicle != null ? System.Text.Json.JsonSerializer.Serialize(existingVehicle) : null,
            null,
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(new { Id = id }, "Arac silindi.");
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateVehicleStatusRequest request, CancellationToken cancellationToken)
    {
        var existingVehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);

        var updatedVehicle = await fleetService.UpdateVehicleStatusAsync(id, request.Status, cancellationToken);
        if (updatedVehicle is null)
        {
            return NotFound(ApiResponse<object>.Fail("Arac bulunamadi."));
        }

        await auditLogService.LogAsync(
            "UpdateStatus",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            existingVehicle != null ? System.Text.Json.JsonSerializer.Serialize(new { existingVehicle.Status }) : null,
            System.Text.Json.JsonSerializer.Serialize(new { Status = request.Status }),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(updatedVehicle, "Arac durumu guncellendi.");
    }

    [HttpPost("{id:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferVehicleRequest request, CancellationToken cancellationToken)
    {
        if (!await fleetService.OfficeExistsAsync(request.TargetOfficeId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir hedef ofis secilmelidir.");
        }

        var existingVehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);

        var updatedVehicle = await fleetService.TransferVehicleAsync(id, request.TargetOfficeId, request.Status, cancellationToken);
        if (updatedVehicle is null)
        {
            return NotFound(ApiResponse<object>.Fail("Arac bulunamadi."));
        }

        await auditLogService.LogAsync(
            "Transfer",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            existingVehicle != null ? System.Text.Json.JsonSerializer.Serialize(new { existingVehicle.OfficeId, existingVehicle.Status }) : null,
            System.Text.Json.JsonSerializer.Serialize(request),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(updatedVehicle, "Arac transfer edildi.");
    }

    [HttpPost("{id:guid}/maintenance")]
    public async Task<IActionResult> ScheduleMaintenance(Guid id, [FromBody] ScheduleVehicleMaintenanceRequest request, CancellationToken cancellationToken)
    {
        if (request.StartDateUtc >= request.EndDateUtc)
        {
            return BadRequestResponse("Bakim baslangic tarihi, bitis tarihinden once olmalidir.");
        }

        if (request.EndDateUtc <= DateTime.UtcNow)
        {
            return BadRequestResponse("Bakim bitis tarihi gelecekte olmalidir.");
        }

        var existingVehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);

        var updatedVehicle = await fleetService.ScheduleVehicleMaintenanceAsync(id, request.StartDateUtc, request.EndDateUtc, request.Notes, cancellationToken);
        if (updatedVehicle is null)
        {
            return NotFound(ApiResponse<object>.Fail("Arac bulunamadi."));
        }

        await auditLogService.LogAsync(
            "Maintenance",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            existingVehicle != null ? System.Text.Json.JsonSerializer.Serialize(new { existingVehicle.Status }) : null,
            System.Text.Json.JsonSerializer.Serialize(request),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(updatedVehicle, "Arac bakima alindi.");
    }

    [HttpPost("{id:guid}/photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(Guid id, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var validationError = ValidatePhotoFile(file);
        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var existingVehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);

        var updatedVehicle = await fleetService.UploadVehiclePhotoAsync(id, file!, cancellationToken);
        if (updatedVehicle is null)
        {
            return NotFound(ApiResponse<object>.Fail("Arac bulunamadi."));
        }

        await auditLogService.LogAsync(
            "UploadPhoto",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            existingVehicle != null ? System.Text.Json.JsonSerializer.Serialize(new { existingVehicle.PhotoUrl }) : null,
            System.Text.Json.JsonSerializer.Serialize(new { FileName = file!.FileName }),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(updatedVehicle, "Arac gorseli yuklendi.");
    }

    private static string? ValidateVehicleInput(string plate, string brand, string model, int year, string color)
    {
        if (string.IsNullOrWhiteSpace(plate) ||
            string.IsNullOrWhiteSpace(brand) ||
            string.IsNullOrWhiteSpace(model) ||
            string.IsNullOrWhiteSpace(color))
        {
            return "Plaka, marka, model ve renk zorunludur.";
        }

        if (year < 1990 || year > DateTime.UtcNow.Year + 1)
        {
            return "Arac yili gecersiz.";
        }

        return null;
    }

    private static string? ValidatePhotoFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "Arac gorseli zorunludur.";
        }

        if (file.Length > MaxPhotoSizeBytes)
        {
            return "Gorsel dosya boyutu izin verilen maksimumu asiyor (5MB).";
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return "Gecersiz gorsel formati. Izin verilen: JPG, PNG, WEBP.";
        }

        return null;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
