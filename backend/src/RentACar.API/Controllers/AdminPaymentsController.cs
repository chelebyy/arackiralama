using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Payments;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/payments")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Payment)]
public sealed class AdminPaymentsController(IPaymentService paymentService) : BaseApiController
{
    [HttpPost("retry")]
    public async Task<IActionResult> RetryPayment(
        [FromBody] AdminPaymentRetryApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ReservationId == Guid.Empty)
        {
            return BadRequestResponse("Geçerli bir rezervasyon ID gereklidir.");
        }

        if (string.IsNullOrWhiteSpace(request.Card.Number) ||
            string.IsNullOrWhiteSpace(request.Card.HolderName))
        {
            return BadRequestResponse("Kart bilgileri zorunludur.");
        }

        try
        {
            var result = await paymentService.RetryPaymentAsync(request, cancellationToken);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail("Rezervasyon bulunamadı."));
            }

            return OkResponse(result, "Ödeme yeniden denemesi oluşturuldu.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetPaymentStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequestResponse("Geçerli bir payment intent ID gereklidir.");
        }

        var result = await paymentService.GetPaymentStatusAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<object>.Fail("Payment intent bulunamadı."));
        }

        return OkResponse(result);
    }
}
