using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.ReservationExtraOptions;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/reservation-extra-options")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminReservationExtraOptionsController(
    IReservationExtraOptionCatalogService catalogService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? vehicleGroupId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await catalogService.GetAdminListAsync(
                search,
                status,
                vehicleGroupId,
                includeArchived,
                page,
                pageSize,
                cancellationToken);
            return OkResponse(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequestResponse(exception.Message);
        }
    }

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateReservationExtraOptionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            () => catalogService.CreateAsync(request, CreateAuditContext(), cancellationToken),
            "Reservation extra option created.");

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateReservationExtraOptionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            () => catalogService.UpdateAsync(id, request, CreateAuditContext(), cancellationToken),
            "Reservation extra option updated.");

    [HttpPatch("{id:guid}/status")]
    public Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateReservationExtraOptionStatusRequest request,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            () => catalogService.UpdateStatusAsync(id, request, CreateAuditContext(), cancellationToken),
            "Reservation extra option status updated.");

    [HttpPost("{id:guid}/restore")]
    public Task<IActionResult> Restore(
        Guid id,
        [FromBody] RestoreReservationExtraOptionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            () => catalogService.RestoreAsync(id, request, CreateAuditContext(), cancellationToken),
            "Reservation extra option restored.");

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(
        Guid id,
        [FromQuery] uint version,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            () => catalogService.DeleteAsync(id, version, CreateAuditContext(), cancellationToken),
            "Reservation extra option delete request completed.");

    private async Task<IActionResult> ExecuteMutationAsync<T>(Func<Task<T>> action, string message)
    {
        try
        {
            return OkResponse(await action(), message);
        }
        catch (ReservationExtraOptionNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail(exception.Message));
        }
        catch (ReservationExtraOptionConcurrencyException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequestResponse(exception.Message);
        }
    }

    private ReservationExtraOptionAuditContext CreateAuditContext() => new(
        User.FindFirstValue(ClaimTypes.NameIdentifier),
        HttpContext.Connection.RemoteIpAddress?.ToString());
}
