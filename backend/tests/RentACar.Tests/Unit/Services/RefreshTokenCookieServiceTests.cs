using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Options;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public class RefreshTokenCookieServiceTests
{
    [Fact]
    public void AppendRefreshTokenCookie_WithSecureDefaults_WritesHttpOnlySecureCookie()
    {
        var service = CreateService();
        var httpContext = CreateHttpContext(isHttps: true);

        service.AppendRefreshTokenCookie(httpContext, "refresh-token", DateTime.UtcNow.AddDays(7));

        var header = httpContext.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        header.Should().Contain("__host-rac_refresh=refresh-token");
        header.Should().Contain("path=/");
        header.Should().Contain("samesite=strict");
        header.Should().Contain("httponly");
        header.Should().Contain("secure");
    }

    [Fact]
    public void AppendRefreshTokenCookie_WithSameAsRequestPolicyOnHttp_DoesNotSetSecureFlag()
    {
        var service = CreateService(new RefreshTokenCookieSettings
        {
            Name = "rac_refresh",
            Path = "/",
            SameSite = SameSiteMode.Strict,
            SecurePolicy = CookieSecurePolicy.SameAsRequest,
            HttpOnly = true,
            IsEssential = true
        });
        var httpContext = CreateHttpContext(isHttps: false);

        service.AppendRefreshTokenCookie(httpContext, "refresh-token", DateTime.UtcNow.AddDays(7));

        var header = httpContext.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        header.Should().Contain("rac_refresh=refresh-token");
        header.Should().NotContain("secure");
    }

    [Fact]
    public void ClearRefreshTokenCookie_DeletesConfiguredCookie()
    {
        var service = CreateService();
        var httpContext = CreateHttpContext(isHttps: true);

        service.ClearRefreshTokenCookie(httpContext);

        var header = httpContext.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        header.Should().Contain("__host-rac_refresh=");
        header.Should().Contain("expires=");
    }

    private static RefreshTokenCookieService CreateService(RefreshTokenCookieSettings? settings = null) =>
        new(Options.Create(settings ?? new RefreshTokenCookieSettings()));

    private static DefaultHttpContext CreateHttpContext(bool isHttps)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = isHttps ? "https" : "http";
        return context;
    }
}
