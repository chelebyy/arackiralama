using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RentACar.API.Middleware;
using RentACar.Infrastructure.Data;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace RentACar.API.Configuration;

public static class ApplicationBuilderExtensions
{
    public static async Task<WebApplication> InitializeApiAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await app.Services.ApplyDatabaseMigrationsAsync(cancellationToken);

        // OpenAPI ve Swagger UI
        app.MapOpenApi();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/openapi/v1.json", "Araç Kiralama API v1");
            c.RoutePrefix = "swagger";
        });

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<CultureMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<IdempotencyMiddleware>();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthChecks("/health", new HealthCheckOptions());
        app.MapControllers();

        return app;
    }
}
