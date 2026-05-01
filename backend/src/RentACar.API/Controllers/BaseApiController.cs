using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;

namespace RentACar.API.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected bool TryReadRefreshToken(string refreshTokenCookieName, out string refreshToken)
    {
        refreshToken = string.Empty;

        if (!HttpContext.Request.Cookies.TryGetValue(refreshTokenCookieName, out var cookieRefreshToken) ||
            string.IsNullOrWhiteSpace(cookieRefreshToken))
        {
            return false;
        }

        refreshToken = cookieRefreshToken.Trim();
        return true;
    }

    protected bool TryReadSessionContext(out Guid principalId, out Guid sessionId)
    {
        principalId = Guid.Empty;
        sessionId = Guid.Empty;

        var principalIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sessionIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value
            ?? User.FindFirst(ClaimTypes.Sid)?.Value;

        return Guid.TryParse(principalIdClaim, out principalId) &&
               Guid.TryParse(sessionIdClaim, out sessionId);
    }

    protected IActionResult OkResponse<T>(T data, string message = "OK") =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult BadRequestResponse(string message) =>
        BadRequest(ApiResponse<object>.Fail(message));

    protected IActionResult UnauthorizedResponse(string message = "Yetkisiz erişim") =>
        Unauthorized(ApiResponse<object>.Fail(message));

    protected IActionResult NotFoundResponse(string message = "Kaynak bulunamadı") =>
        NotFound(ApiResponse<object>.Fail(message));
}
