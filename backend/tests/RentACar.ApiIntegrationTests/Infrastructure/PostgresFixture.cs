using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RentACar.Infrastructure.Data;
using Xunit;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Creates a dedicated PostgreSQL database for a test class and applies migrations.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>
    /// Gets the unique database name used by the current test class.
    /// </summary>
    public string DatabaseName { get; } = $"rentacar_test_{Guid.NewGuid():N}";

    /// <summary>
    /// Gets the PostgreSQL connection string for the test database.
    /// </summary>
    public string ConnectionString { get; }

    private string AdminConnectionString { get; }

    public PostgresFixture()
    {
        var baseConnectionString = ResolveBaseConnectionString();
        ConnectionString = ReplaceDatabase(baseConnectionString, DatabaseName);
        AdminConnectionString = ReplaceDatabase(baseConnectionString, "postgres");
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await using var adminContext = CreateContext(AdminConnectionString);
#pragma warning disable EF1002
        await adminContext.Database.ExecuteSqlRawAsync($"CREATE DATABASE \"{DatabaseName}\"");
#pragma warning restore EF1002

        await using var migratedContext = CreateContext(ConnectionString);
        await migratedContext.Database.MigrateAsync();
        await TestDataSeeder.SeedAsync(migratedContext);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await using var adminContext = CreateContext(AdminConnectionString);
#pragma warning disable EF1002
        await adminContext.Database.ExecuteSqlRawAsync(
            $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{DatabaseName}'
              AND pid <> pg_backend_pid();
            """);

        await adminContext.Database.ExecuteSqlRawAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\"");
#pragma warning restore EF1002
    }

    private static string ResolveBaseConnectionString()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable("RENTACAR_TEST_POSTGRES")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        return "Host=localhost;Port=5433;Database=rentacar;Username=postgres;Password=postgres";
    }

    private static string ReplaceDatabase(string connectionString, string databaseName)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (builder.ContainsKey("Database"))
        {
            builder["Database"] = databaseName;
        }
        else if (builder.ContainsKey("Initial Catalog"))
        {
            builder["Initial Catalog"] = databaseName;
        }
        else
        {
            builder["Database"] = databaseName;
        }

        return builder.ConnectionString;
    }

    private static RentACarDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RentACarDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(RentACarDbContext).Assembly.FullName))
            .Options;

        return new RentACarDbContext(options);
    }

}
