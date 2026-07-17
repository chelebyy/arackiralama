namespace RentACar.API.Middleware;

public sealed class PublicCustomerAccountSurfaceMiddleware(RequestDelegate next)
{
    private static readonly PathString RegisterPath = new("/api/customer/v1/auth/register");
    private static readonly PathString RegisterTrailingSlashPath = new("/api/customer/v1/auth/register/");
    private static readonly PathString ClaimPath = new("/api/customer/v1/auth/claim");
    private static readonly PathString ClaimTrailingSlashPath = new("/api/customer/v1/auth/claim/");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(RegisterPath)
            || context.Request.Path.Equals(RegisterTrailingSlashPath)
            || context.Request.Path.Equals(ClaimPath)
            || context.Request.Path.Equals(ClaimTrailingSlashPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }
}
