using StackExchange.Redis;
using Xunit;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Provides a shared Redis connection for API integration tests.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    /// <summary>
    /// Gets the resolved Redis connection string.
    /// </summary>
    public string ConnectionString { get; } = ResolveConnectionString();

    /// <summary>
    /// Gets the shared Redis connection multiplexer.
    /// </summary>
    public IConnectionMultiplexer ConnectionMultiplexer { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether Redis accepted a live ping during fixture setup.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var configuration = ConfigurationOptions.Parse(ConnectionString);
        configuration.AbortOnConnectFail = false;

        ConnectionMultiplexer = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(configuration);

        try
        {
            await ConnectionMultiplexer.GetDatabase().PingAsync();
            IsAvailable = true;
        }
        catch (RedisConnectionException)
        {
            IsAvailable = false;
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (ConnectionMultiplexer is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        ConnectionMultiplexer.Dispose();
    }

    /// <summary>
    /// Creates an isolated Redis scope for a test class.
    /// </summary>
    public RedisTestScope CreateScope() =>
        new($"test:{Guid.NewGuid():N}:");

    private static string ResolveConnectionString() =>
        Environment.GetEnvironmentVariable("RENTACAR_TEST_REDIS")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
        ?? Environment.GetEnvironmentVariable("Redis__ConnectionString")
        ?? "localhost:6379";
}

/// <summary>
/// Represents an isolated Redis namespace for a test class.
/// </summary>
/// <param name="KeyPrefix">The unique key prefix used by the test class.</param>
public sealed record RedisTestScope(string KeyPrefix);
