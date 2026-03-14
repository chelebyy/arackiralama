using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Payments;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/payments")]
[EnableRateLimiting(RateLimitPolicyNames.Payment)]
public sealed class PaymentsController(IPaymentService paymentService) : BaseApiController
{
    [HttpPost("intents")]
    public async Task<IActionResult> CreateIntent(
        [FromBody] CreatePaymentIntentApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ReservationId == Guid.Empty)
        {
            return BadRequestResponse("Geçerli bir rezervasyon ID gereklidir.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return BadRequestResponse("Idempotency key zorunludur.");
        }

        try
        {
            var result = await paymentService.CreateIntentAsync(request, cancellationToken);
            if (result == null)
            {
                return NotFound("Rezervasyon bulunamadı.");
            }

            return OkResponse(result, "Payment intent oluşturuldu.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("{intentId:guid}/3ds-return")]
    public async Task<IActionResult> CompleteThreeDs(
        Guid intentId,
        [FromBody] ThreeDsReturnApiRequest request,
        CancellationToken cancellationToken)
    {
        if (intentId == Guid.Empty)
        {
            return BadRequestResponse("Geçerli bir payment intent ID gereklidir.");
        }

        if (string.IsNullOrWhiteSpace(request.BankResponse))
        {
            return BadRequestResponse("Banka dönüş verisi boş olamaz.");
        }

        var result = await paymentService.CompleteThreeDsAsync(intentId, request, cancellationToken);
        if (result == null)
        {
            return NotFound("Payment intent bulunamadı.");
        }

        return OkResponse(result, "3D Secure doğrulaması işlendi.");
    }

    [HttpPost("webhook/{provider}")]
    public async Task<IActionResult> HandleWebhook(
        string provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequestResponse("Provider bilgisi zorunludur.");
        }

        Request.EnableBuffering();
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return BadRequestResponse("Webhook payload boş olamaz.");
        }

        var signature = Request.Headers["X-Webhook-Signature"].ToString();
        var timestamp = Request.Headers["X-Webhook-Timestamp"].ToString();
        var eventType = Request.Headers["X-Webhook-Event"].ToString();

        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequestResponse("Webhook imza başlığı eksik.");
        }

        try
        {
            var result = await paymentService.ProcessWebhookAsync(
                provider,
                payload,
                signature,
                string.IsNullOrWhiteSpace(timestamp) ? null : timestamp,
                string.IsNullOrWhiteSpace(eventType) ? null : eventType,
                cancellationToken);

            var message = result.Duplicate
                ? "Webhook duplicate event ignored."
                : "Webhook processed.";

            return OkResponse(result, message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }
}
