using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

/// <summary>
/// Customer-specific reservation operations with resource-based authorization.
/// Customers can only access their own reservations.
/// </summary>
[Route("api/customer/v1/reservations")]
[Authorize(Policy = AuthPolicyNames.CustomerOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class CustomerReservationsController(
    IReservationService reservationService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMyReservations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadPrincipalId(out var customerId))
        {
            return UnauthorizedResponse();
        }

        var result = await reservationService.GetCustomerReservationsPaginatedAsync(
            customerId, page, pageSize, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMyReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(out var customerId))
        {
            return UnauthorizedResponse();
        }

        var reservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);

        if (reservation is null)
        {
            return NotFoundResponse("Rezervasyon bulunamadi.");
        }

        if (reservation.CustomerId != customerId)
        {
            return NotFoundResponse("Rezervasyon bulunamadi.");
        }

        return OkResponse(reservation);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelMyReservation(
        Guid id,
        [FromBody] string? reason,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(out var customerId))
        {
            return UnauthorizedResponse();
        }

        var reservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);

        if (reservation is null)
        {
            return NotFoundResponse("Rezervasyon bulunamadi.");
        }

        if (reservation.CustomerId != customerId)
        {
            return NotFoundResponse("Rezervasyon bulunamadi.");
        }

        var success = await reservationService.CancelReservationAsync(id, reason, cancellationToken);

        if (!success)
        {
            return BadRequestResponse("Rezervasyon iptal edilemedi.");
        }

        return OkResponse<object?>(null, "Rezervasyon basariyla iptal edildi.");
    }

    private bool TryReadPrincipalId(out Guid principalId)
    {
        principalId = Guid.Empty;

        var principalIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(principalIdClaim, out principalId);
    }
}
