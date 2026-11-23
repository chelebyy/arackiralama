using Microsoft.AspNetCore.Mvc;
using RentACar.API.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;

namespace RentACar.API.Controllers;

[ApiController]
[RequestSizeLimit(8192)]
[Route("api/guest/v1/reservation")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GuestReservationsController(GuestReservationService service) : ControllerBase
{
    [HttpPost("request")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> RequestAccess(GuestAccessRequest request, CancellationToken ct)
    {
        var challengeId = await service.RequestAccessAsync(request, ct);
        return Ok(new { challengeId });
    }

    [HttpPost("verify")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Verify(GuestVerificationRequest request, CancellationToken ct)
    {
        var session = await service.VerifyAsync(request, ct);
        return session is null ? Unauthorized(new { code = "access_invalid" }) : Ok(session);
    }

    [HttpGet]
    public Task<IActionResult> View(CancellationToken ct) => Authorized(false, ct, a => service.ViewAsync(a, ct));

    [HttpPost("logout")]
    public Task<IActionResult> Logout(CancellationToken ct) => Authorized(true, ct, async a =>
    {
        await service.LogoutAsync(a, ct);
        return new { success = true };
    });

    [HttpPost("cancel")]
    public Task<IActionResult> Cancel(GuestCancellationRequest request, CancellationToken ct) =>
        Authorized(true, ct, a => service.CancelAsync(a, request, ct));

    [HttpPost("amendment/quote")]
    public Task<IActionResult> Quote(GuestAmendmentRequest request, CancellationToken ct) =>
        Authorized(true, ct, a => service.QuoteAmendmentAsync(a, request, ct));

    [HttpPost("amendment/confirm")]
    public Task<IActionResult> Confirm(GuestAmendmentConfirmation request, CancellationToken ct) =>
        Authorized(true, ct, a => service.ConfirmAmendmentAsync(a, request, ct));

    private async Task<IActionResult> Authorized(bool mutation, CancellationToken ct,
        Func<GuestReservationAccess, Task<object>> operation)
    {
        var access = await service.AuthenticateAsync(Request.Headers["X-Guest-Session"].FirstOrDefault(),
            Request.Headers["X-Guest-CSRF"].FirstOrDefault(), mutation, ct);
        if (access is null) return Unauthorized(new { code = "access_invalid" });
        try { return Ok(await operation(access)); }
        catch (ReservationQuoteConflictException) { return Conflict(new { code = "conditions_changed" }); }
        catch (DbUpdateException) { return Conflict(new { code = "conditions_changed" }); }
        catch (ArgumentException) { return BadRequest(new { code = "invalid_request" }); }
    }
}
