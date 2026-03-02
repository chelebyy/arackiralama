using System.Text.Json;
using RentACar.API.Contracts;

namespace RentACar.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception occurred");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var payload = JsonSerializer.Serialize(ApiResponse<object>.Fail("Beklenmeyen bir hata oluştu."));
            await context.Response.WriteAsync(payload);
        }
    }
}
