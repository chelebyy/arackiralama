using Microsoft.EntityFrameworkCore;
using RentACar.Infrastructure.Data;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Resets all mapped tables in the integration test database.
/// </summary>
public static class DatabaseReset
{
    /// <summary>
    /// Truncates all entity tables and restarts identities.
    /// </summary>
    public static async Task ResetAsync(RentACarDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var tableNames = dbContext.Model.GetEntityTypes()
            .Select(entityType => new
            {
                Schema = entityType.GetSchema() ?? "public",
                Table = entityType.GetTableName()
            })
            .Where(table => !string.IsNullOrWhiteSpace(table.Table))
            .Select(table => $"\"{table.Schema}\".\"{table.Table}\"")
            .Distinct()
            .ToArray();

        if (tableNames.Length == 0)
        {
            return;
        }

        var truncateCommand = $"TRUNCATE TABLE {string.Join(", ", tableNames)} RESTART IDENTITY CASCADE;";
        await dbContext.Database.ExecuteSqlRawAsync(truncateCommand, cancellationToken);
    }
}
