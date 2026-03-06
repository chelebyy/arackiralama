using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/offices")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminOfficesController(IFleetService fleetService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var offices = await fleetService.GetOfficesAsync(cancellationToken);
        return OkResponse(offices);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfficeRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateOfficeInput(request.Name, request.Address, request.Phone, request.OpeningHours);
        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var createdOffice = await fleetService.CreateOfficeAsync(request, cancellationToken);
        return OkResponse(createdOffice, "Ofis olusturuldu.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfficeRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateOfficeInput(request.Name, request.Address, request.Phone, request.OpeningHours);
        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var updatedOffice = await fleetService.UpdateOfficeAsync(id, request, cancellationToken);
        if (updatedOffice is null)
        {
            return NotFound(ApiResponse<object>.Fail("Ofis bulunamadi."));
        }

        return OkResponse(updatedOffice, "Ofis guncellendi.");
    }

    private static string? ValidateOfficeInput(string name, string address, string phone, string openingHours)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(address) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(openingHours))
        {
            return "Isim, adres, telefon ve calisma saatleri zorunludur.";
        }

        return null;
    }
}
