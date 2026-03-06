using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RentACar.Infrastructure.Data;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var autoMigrateSetting = configuration["Database:AutoMigrateOnStartup"];
        var autoMigrateOnStartup = !bool.TryParse(autoMigrateSetting, out var parsedAutoMigrate) || parsedAutoMigrate;

        if (!autoMigrateOnStartup)
        {
            logger.LogInformation("Database auto-migration is disabled.");
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<RentACarDbContext>();
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                logger.LogInformation("Applying pending database migrations. Attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations completed successfully.");
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
                logger.LogWarning(exception, "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds} seconds.", attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        logger.LogError("Database migrations failed after {MaxAttempts} attempts.", maxAttempts);
        throw new InvalidOperationException("Database migrations could not be applied on startup.");
    }
}



