using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentACar.Infrastructure.Data.Configurations;

namespace RentACar.Infrastructure.Data;

public static class ConcurrentBookingInventorySeedExtensions
{
    public static async Task ApplyConcurrentBookingInventorySeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ConcurrentBookingInventorySeed");
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        if (!configuration.GetValue<bool>("LoadTesting:ConcurrentBookingInventorySeedEnabled"))
        {
            logger.LogInformation("Concurrent booking inventory seed is disabled.");
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<RentACarDbContext>();
        var seededAtUtc = SeedDataConstants.SeededAtUtc;
        var sql = BuildSeedSql(seededAtUtc);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Concurrent booking inventory seed applied.");
    }

    private static string BuildSeedSql(DateTime seededAtUtc)
    {
        var sql = new StringBuilder();

        for (var i = 1; i <= 120; i++)
        {
            var vehicleId = Guid.Parse($"44444444-4444-4444-4444-{i:D12}");
            var plate = $"34LT{i:000}";
            var brand = i % 2 == 0 ? "Renault" : "Fiat";
            var model = i % 2 == 0 ? "Clio" : "Egea";
            var color = (i % 3) switch
            {
                0 => "White",
                1 => "Gray",
                _ => "Black"
            };
            var year = 2022 + (i % 3);

            sql.AppendLine($"""
                INSERT INTO vehicles (
                    id,
                    brand,
                    color,
                    created_at,
                    group_id,
                    model,
                    office_id,
                    photo_url,
                    plate,
                    status,
                    updated_at,
                    year)
                VALUES (
                    '{vehicleId}',
                    '{brand}',
                    '{color}',
                    '{seededAtUtc:O}',
                    '{SeedDataConstants.EconomyGroupId}',
                    '{model}',
                    '{SeedDataConstants.AlanyaCenterOfficeId}',
                    NULL,
                    '{plate}',
                    'Available',
                    '{seededAtUtc:O}',
                    {year})
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        return sql.ToString();
    }
}
