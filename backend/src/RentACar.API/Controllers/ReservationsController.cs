using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Attributes;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/reservations")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class ReservationsController(IReservationService reservationService) : BaseApiController
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    [Idempotent(ExpirationHours = 24)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.VehicleGroupId == Guid.Empty)
        {
            return BadRequestResponse("Geçerli bir araç grubu seçilmelidir.");
        }

        if (request.PickupOfficeId == Guid.Empty)
        {
            return BadRequestResponse("Geçerli bir alış ofisi seçilmelidir.");
        }

        if (request.PickupDateTimeUtc >= request.ReturnDateTimeUtc)
        {
            return BadRequestResponse("Alış tarihi dönüş tarihinden önce olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(request.Customer.Email))
        {
            return BadRequestResponse("Müşteri e-posta adresi gereklidir.");
        }

        try
        {
            var reservation = await reservationService.CreateDraftReservationAsync(request, cancellationToken);
            return OkResponse(reservation, "Rezervasyon başarıyla oluşturuldu.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpGet("{publicCode}")]
    public async Task<IActionResult> GetByPublicCode(
        string publicCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicCode))
        {
            return BadRequestResponse("Rezervasyon kodu gereklidir.");
        }

        var reservation = await reservationService.GetReservationByPublicCodeAsync(publicCode, cancellationToken);
        
        if (reservation == null)
        {
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
        }

        return OkResponse(reservation);
    }

    [HttpPost("{reservationId:guid}/hold")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> CreateHold(
        Guid reservationId,
        [FromHeader(Name = "X-Session-Id")] string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequestResponse("Oturum kimliği gereklidir (X-Session-Id header).");
        }

        try
        {
            var hold = await reservationService.CreateHoldAsync(reservationId, sessionId, cancellationToken);
            
            if (hold == null)
            {
                return BadRequestResponse("Rezervasyon tutma işlemi başarısız oldu.");
            }

            return OkResponse(hold, "Rezervasyon 15 dakika süreyle tutuldu.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{reservationId:guid}/hold/extend")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> ExtendHold(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var hold = await reservationService.ExtendHoldAsync(reservationId, cancellationToken);
        
        if (hold == null)
        {
            return BadRequestResponse("Tutma süresi uzatılamadı. Süre dolmuş veya geçersiz rezervasyon.");
        }

        return OkResponse(hold, "Tutma süresi 5 dakika uzatıldı.");
    }

    [HttpDelete("{reservationId:guid}/hold")]
    public async Task<IActionResult> ReleaseHold(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var success = await reservationService.ReleaseHoldByReservationIdAsync(reservationId, cancellationToken);
        
        if (!success)
        {
            return BadRequestResponse("Tutma serbest bırakılamadı.");
        }

        return OkResponse<object?>(null, "Rezervasyon tutması serbest bırakıldı.");
    }

    [HttpPost("{reservationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid reservationId,
        [FromBody] string? reason,
        CancellationToken cancellationToken)
    {
        var success = await reservationService.CancelReservationAsync(reservationId, reason, cancellationToken);
        
        if (!success)
        {
            return BadRequestResponse("Rezervasyon iptal edilemedi.");
        }

        return OkResponse<object?>(null, "Rezervasyon başarıyla iptal edildi.");
    }
}
