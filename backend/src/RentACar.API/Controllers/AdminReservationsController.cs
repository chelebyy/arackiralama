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
    IPaymentService paymentService) : BaseApiController
{
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
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
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
            var reservation = await reservationService.UpdateReservationAsync(id, request, cancellationToken);
            
            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(reservation, "Rezervasyon başarıyla güncellendi.");
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
            return BadRequestResponse("Geçerli bir araç ID'si gereklidir.");
        }

        try
        {
            var reservation = await reservationService.AssignVehicleAsync(id, vehicleId, cancellationToken);
            
            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(reservation, "Araç rezervasyona atandı.");
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
        var reservation = await reservationService.UnassignVehicleAsync(id, cancellationToken);
        
        if (reservation == null)
        {
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
        }

        return OkResponse(reservation, "Araç rezervasyondan kaldırıldı.");
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
            return BadRequestResponse("Geçersiz durum değeri.");
        }

        try
        {
            var reservation = await reservationService.TransitionStatusAsync(id, status, cancellationToken);
            
            if (reservation == null)
            {
                return BadRequestResponse("Durum geçişi yapılamadı. Geçersiz rezervasyon veya durum.");
            }

            return OkResponse(reservation, $"Rezervasyon durumu '{newStatus}' olarak güncellendi.");
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
            var reservation = await reservationService.CheckInAsync(id, request, cancellationToken);
            
            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(reservation, "Rezervasyon check-in yapıldı.");
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
            var reservation = await reservationService.CheckOutAsync(id, request, cancellationToken);
            
            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(reservation, "Rezervasyon check-out yapıldı.");
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
            var reservation = await reservationService.AdminCancelReservationAsync(id, reason, cancellationToken);
            
            if (reservation == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(reservation, "Rezervasyon yönetici tarafından iptal edildi.");
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
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(result, "İade işlemi tamamlandı.");
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
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(result, "Depozito bırakma işlemi tamamlandı.");
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
        return OkResponse<object?>(null, "Süresi dolan rezervasyonlar işlendi.");
    }

    [HttpGet("status-transitions/{currentStatus}")]
    public async Task<IActionResult> GetValidStatusTransitions(
        string currentStatus,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReservationStatus>(currentStatus, true, out var status))
        {
            return BadRequestResponse("Geçersiz durum değeri.");
        }

        var validTransitions = await reservationService.GetValidNextStatusesAsync(status, cancellationToken);
        return OkResponse(validTransitions);
    }
}
