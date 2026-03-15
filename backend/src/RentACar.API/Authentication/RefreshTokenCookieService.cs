using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RentACar.API.Options;

namespace RentACar.API.Authentication;

public sealed class RefreshTokenCookieService(IOptions<RefreshTokenCookieSettings> settings) : IRefreshTokenCookieService
{
    private readonly RefreshTokenCookieSettings _settings = settings.Value;

    public void AppendRefreshTokenCookie(HttpContext httpContext, string refreshToken, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token cannot be null or empty.", nameof(refreshToken));
        }

        httpContext.Response.Cookies.Append(_settings.Name, refreshToken, BuildCookieOptions(httpContext, expiresAtUtc));
    }

    public void ClearRefreshTokenCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(_settings.Name, BuildCookieOptions(httpContext, DateTimeOffset.UnixEpoch.UtcDateTime));
    }

    private CookieOptions BuildCookieOptions(HttpContext httpContext, DateTime expiresAtUtc) =>
        new()
        {
            HttpOnly = _settings.HttpOnly,
            Secure = ResolveSecureFlag(httpContext.Request.IsHttps),
            SameSite = _settings.SameSite,
            Expires = new DateTimeOffset(expiresAtUtc),
            Path = _settings.Path,
            Domain = _settings.Domain,
            IsEssential = _settings.IsEssential
        };

    private bool ResolveSecureFlag(bool isHttpsRequest) =>
        _settings.SecurePolicy switch
        {
            CookieSecurePolicy.Always => true,
            CookieSecurePolicy.None => false,
            CookieSecurePolicy.SameAsRequest => isHttpsRequest,
            _ => true
        };
}
