using Xunit;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Groups API integration tests behind a shared Redis connection and disables parallel execution.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<RedisFixture>
{
    /// <summary>
    /// Shared collection name for API integration tests.
    /// </summary>
    public const string Name = "api-integration";
}
