using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Payments;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Enums;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/reservations")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminReservationsController(
    IReservationService reservationService,
    IPaymentService paymentService,
    IAuditLogService auditLogService) : BaseApiController
{
    private const string EntityType = "Reservation";

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ReservationFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        var reservations = await reservationService.GetAllReservationsAsync(filter, cancellationToken);
        return OkResponse(reservations);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);

        if (reservation == null)
        {
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
        }

        return OkResponse(reservation);
    }

    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomerId(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var reservations = await reservationService.GetCustomerReservationsAsync(customerId, cancellationToken);
        return OkResponse(reservations);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateReservationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
            if (existingReservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            var reservation = await reservationService.UpdateReservationAsync(id, request, cancellationToken);

            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "Update",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                System.Text.Json.JsonSerializer.Serialize(existingReservation),
                System.Text.Json.JsonSerializer.Serialize(reservation),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(reservation, "Rezervasyon basariyla guncellendi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(
        Guid id,
        [FromBody] Guid vehicleId,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty)
        {
            return BadRequestResponse("Gecerli bir arac ID'si gereklidir.");
        }

        try
        {
            var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
            if (existingReservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            var reservation = await reservationService.AssignVehicleAsync(id, vehicleId, cancellationToken);

            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "AssignVehicle",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                System.Text.Json.JsonSerializer.Serialize(new { existingReservation.VehicleId }),
                System.Text.Json.JsonSerializer.Serialize(new { VehicleId = vehicleId }),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(reservation, "Arac rezervasyona atandi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/unassign-vehicle")]
    public async Task<IActionResult> UnassignVehicle(
        Guid id,
        CancellationToken cancellationToken)
    {
        var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
        if (existingReservation == null)
        {
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
        }

        var reservation = await reservationService.UnassignVehicleAsync(id, cancellationToken);

        if (reservation == null)
        {
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
        }

        await auditLogService.LogAsync(
            "UnassignVehicle",
            EntityType,
            id.ToString(),
            GetCurrentUserId(),
            System.Text.Json.JsonSerializer.Serialize(new { existingReservation.VehicleId }),
            System.Text.Json.JsonSerializer.Serialize(new { VehicleId = (Guid?)null }),
            GetClientIpAddress(),
            cancellationToken);

        return OkResponse(reservation, "Arac rezervasyondan kaldirildi.");
    }

    [HttpPost("{id:guid}/transition-status")]
    public async Task<IActionResult> TransitionStatus(
        Guid id,
        [FromBody] string newStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            return BadRequestResponse("Yeni durum gereklidir.");
        }

        if (!Enum.TryParse<ReservationStatus>(newStatus, true, out var status))
        {
            return BadRequestResponse("Gecersiz durum degeri.");
        }

        try
        {
            var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
            if (existingReservation == null)
            {
                return BadRequestResponse("Durum gecisi yapilamadi. Gecersiz rezervasyon veya durum.");
            }

            var reservation = await reservationService.TransitionStatusAsync(id, status, cancellationToken);

            if (reservation == null)
            {
                return BadRequestResponse("Durum gecisi yapilamadi. Gecersiz rezervasyon veya durum.");
            }

            await auditLogService.LogAsync(
                "TransitionStatus",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                System.Text.Json.JsonSerializer.Serialize(new { existingReservation.Status }),
                System.Text.Json.JsonSerializer.Serialize(new { Status = newStatus }),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(reservation, $"Rezervasyon durumu '{newStatus}' olarak guncellendi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(
        Guid id,
        [FromBody] CheckInRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
            if (existingReservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            var reservation = await reservationService.CheckInAsync(id, request, cancellationToken);

            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "CheckIn",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                System.Text.Json.JsonSerializer.Serialize(new { existingReservation.Status }),
                System.Text.Json.JsonSerializer.Serialize(new { reservation.Status, request.ActualMileage, request.ActualFuelLevel, request.Notes }),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(reservation, "Rezervasyon check-in yapildi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/check-out")]
    public async Task<IActionResult> CheckOut(
        Guid id,
        [FromBody] CheckOutRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
            if (existingReservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            var reservation = await reservationService.CheckOutAsync(id, request, cancellationToken);

            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "CheckOut",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                System.Text.Json.JsonSerializer.Serialize(new { existingReservation.Status }),
                System.Text.Json.JsonSerializer.Serialize(new { reservation.Status, request.ReturnMileage, request.ReturnFuelLevel, request.Notes }),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(reservation, "Rezervasyon check-out yapildi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var existingReservation = await reservationService.GetReservationByIdAsync(id, cancellationToken);
            if (existingReservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            var reservation = await reservationService.AdminCancelReservationAsync(id, reason, cancellationToken);

            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "Cancel",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                System.Text.Json.JsonSerializer.Serialize(new { existingReservation.Status }),
                System.Text.Json.JsonSerializer.Serialize(new { Status = reservation.Status, Reason = reason }),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(reservation, "Rezervasyon yonetici tarafindan iptal edildi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] AdminRefundApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await paymentService.RefundReservationAsync(id, request, cancellationToken);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "Refund",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                null,
                System.Text.Json.JsonSerializer.Serialize(request),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(result, "Iade islemi tamamlandi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{id:guid}/release-deposit")]
    public async Task<IActionResult> ReleaseDeposit(
        Guid id,
        [FromBody] AdminReleaseDepositApiRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await paymentService.ReleaseDepositAsync(id, request?.Note, cancellationToken);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadi."));
            }

            await auditLogService.LogAsync(
                "ReleaseDeposit",
                EntityType,
                id.ToString(),
                GetCurrentUserId(),
                null,
                System.Text.Json.JsonSerializer.Serialize(new { Note = request?.Note }),
                GetClientIpAddress(),
                cancellationToken);

            return OkResponse(result, "Depozito birakma islemi tamamlandi.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("process-expired")]
    public async Task<IActionResult> ProcessExpired(CancellationToken cancellationToken)
    {
        await reservationService.ProcessExpiredReservationsAsync(cancellationToken);
        return OkResponse<object?>(null, "Suresi dolan rezervasyonlar islendi.");
    }

    [HttpGet("status-transitions/{currentStatus}")]
    public async Task<IActionResult> GetValidStatusTransitions(
        string currentStatus,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReservationStatus>(currentStatus, true, out var status))
        {
            return BadRequestResponse("Gecersiz durum degeri.");
        }

        var validTransitions = await reservationService.GetValidNextStatusesAsync(status, cancellationToken);
        return OkResponse(validTransitions);
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
