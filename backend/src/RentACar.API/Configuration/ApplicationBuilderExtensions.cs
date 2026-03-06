using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RentACar.API.Middleware;
using RentACar.Infrastructure.Data;

namespace RentACar.API.Configuration;

public static class ApplicationBuilderExtensions
{
    public static async Task<WebApplication> InitializeApiAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await app.Services.ApplyDatabaseMigrationsAsync(cancellationToken);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<CultureMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthChecks("/health", new HealthCheckOptions());
        app.MapControllers();

        return app;
    }
}
