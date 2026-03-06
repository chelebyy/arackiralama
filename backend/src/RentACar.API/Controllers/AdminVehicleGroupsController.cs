using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/vehicle-groups")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminVehicleGroupsController(IFleetService fleetService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var vehicleGroups = await fleetService.GetVehicleGroupsAsync(cancellationToken);
        return OkResponse(vehicleGroups);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleGroupRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateVehicleGroupInput(
            request.NameTr,
            request.NameEn,
            request.NameRu,
            request.NameAr,
            request.NameDe,
            request.DepositAmount,
            request.MinAge,
            request.MinLicenseYears);

        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var createdVehicleGroup = await fleetService.CreateVehicleGroupAsync(request, cancellationToken);
        return OkResponse(createdVehicleGroup, "Arac grubu olusturuldu.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleGroupRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateVehicleGroupInput(
            request.NameTr,
            request.NameEn,
            request.NameRu,
            request.NameAr,
            request.NameDe,
            request.DepositAmount,
            request.MinAge,
            request.MinLicenseYears);

        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var updatedVehicleGroup = await fleetService.UpdateVehicleGroupAsync(id, request, cancellationToken);
        if (updatedVehicleGroup is null)
        {
            return NotFound(ApiResponse<object>.Fail("Arac grubu bulunamadi."));
        }

        return OkResponse(updatedVehicleGroup, "Arac grubu guncellendi.");
    }

    private static string? ValidateVehicleGroupInput(
        string nameTr,
        string nameEn,
        string nameRu,
        string nameAr,
        string nameDe,
        decimal depositAmount,
        int minAge,
        int minLicenseYears)
    {
        if (string.IsNullOrWhiteSpace(nameTr) ||
            string.IsNullOrWhiteSpace(nameEn) ||
            string.IsNullOrWhiteSpace(nameRu) ||
            string.IsNullOrWhiteSpace(nameAr) ||
            string.IsNullOrWhiteSpace(nameDe))
        {
            return "Tum dil alanlari zorunludur.";
        }

        if (depositAmount < 0)
        {
            return "Depozito tutari negatif olamaz.";
        }

        if (minAge < 18)
        {
            return "Minimum yas 18 veya daha buyuk olmalidir.";
        }

        if (minLicenseYears < 0)
        {
            return "Minimum ehliyet yili negatif olamaz.";
        }

        return null;
    }
}
