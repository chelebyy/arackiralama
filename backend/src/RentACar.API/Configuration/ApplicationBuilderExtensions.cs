using Microsoft.AspNetCore.Builder;
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
        await app.Services.ApplyLocalAdminSeedAsync(cancellationToken);
        await app.Services.ApplyConcurrentBookingInventorySeedAsync(cancellationToken);
        await app.Services.ApplyReservationExtraOptionsBackfillAsync(cancellationToken);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/openapi/v1.json", "Araç Kiralama API v1");
                c.RoutePrefix = "swagger";
            });
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (!app.Environment.IsDevelopment())
                {
                    context.Response.Headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
                }

                context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'; object-src 'none'");
                context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
                context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
                context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
                return Task.CompletedTask;
            });

            await next(context);
        });

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<CultureMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<IdempotencyMiddleware>();
        app.UseStaticFiles();
        app.UseCors(ServiceCollectionExtensions.ApiCorsPolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthChecks("/health", new HealthCheckOptions());
        app.MapControllers();

        return app;
    }
}
