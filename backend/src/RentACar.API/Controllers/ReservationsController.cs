using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Attributes;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;

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
        CancellationToken cancellationToken,
        [FromHeader(Name = "X-Session-Id")] string? sessionId = null,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
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
            request = request with
            {
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? request.SessionId : sessionId.Trim(),
                IdempotencyKey = idempotencyKey?.Trim()
            };
            var reservation = await reservationService.CreateDraftReservationAsync(request, cancellationToken);
            return OkResponse(reservation, "Rezervasyon başarıyla oluşturuldu.");
        }
        catch (ReservationQuoteConflictException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("unpaid-requests")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    [Idempotent(ExpirationHours = 24)]
    public async Task<IActionResult> CreateUnpaidRequest(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken,
        [FromHeader(Name = "X-Session-Id")] string? sessionId = null,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
    {
        if (vehicleGroupOrOfficeInvalid(request))
        {
            return BadRequestResponse("Geçerli bir araç grubu ve alış ofisi seçilmelidir.");
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
            request = request with
            {
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? request.SessionId : sessionId.Trim(),
                IdempotencyKey = idempotencyKey?.Trim()
            };
            var reservation = await reservationService.CreateUnpaidRequestAsync(request, cancellationToken);
            return OkResponse(reservation, "Talebiniz alındı. Araç 24 saat süreyle bloke edildi.");
        }
        catch (ReservationQuoteConflictException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }

        static bool vehicleGroupOrOfficeInvalid(CreateReservationRequest request)
        {
            return request.VehicleGroupId == Guid.Empty || request.PickupOfficeId == Guid.Empty;
        }
    }

    [HttpGet("{publicCode}")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetByPublicCode(
        string publicCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicCode))
        {
            return BadRequestResponse("Rezervasyon kodu gereklidir.");
        }

        if (publicCode.Length > Reservation.PublicCodeMaxLength)
        {
            return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
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
}
