using System.Net;
using FluentAssertions;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests;

/// <summary>
/// Verifies that the API boots successfully through the full HTTP pipeline.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class HealthSmokeTests(RedisFixture redisFixture) : IAsyncLifetime
{
    private readonly RedisFixture _redisFixture = redisFixture;
    private PostgresFixture? _postgresFixture;
    private ApiWebApplicationFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _postgresFixture = new PostgresFixture();
        await _postgresFixture.InitializeAsync();

        var redisScope = _redisFixture.CreateScope();
        _factory = new ApiWebApplicationFactory(_postgresFixture, _redisFixture, redisScope.KeyPrefix);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgresFixture is not null)
        {
            await _postgresFixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client!.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
