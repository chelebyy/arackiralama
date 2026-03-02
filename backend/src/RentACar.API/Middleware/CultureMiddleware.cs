using System.Globalization;

namespace RentACar.API.Middleware;

public class CultureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var culture = context.Request.Headers["X-Culture"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(culture))
        {
            culture = context.Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',').FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(culture))
        {
            culture = "tr-TR";
        }

        try
        {
            var cultureInfo = new CultureInfo(culture);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
        }
        catch (CultureNotFoundException)
        {
            var fallback = new CultureInfo("tr-TR");
            CultureInfo.CurrentCulture = fallback;
            CultureInfo.CurrentUICulture = fallback;
        }

        await next(context);
    }
}
