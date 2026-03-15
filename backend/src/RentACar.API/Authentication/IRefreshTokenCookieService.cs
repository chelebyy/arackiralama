using Microsoft.AspNetCore.Http;

namespace RentACar.API.Authentication;

public interface IRefreshTokenCookieService
{
    void AppendRefreshTokenCookie(HttpContext httpContext, string refreshToken, DateTime expiresAtUtc);
    void ClearRefreshTokenCookie(HttpContext httpContext);
}
