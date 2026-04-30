using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Infrastructure.Data;
using Xunit;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Base class for endpoint-level integration tests using a real API pipeline.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class ApiIntegrationTestBase(RedisFixture redisFixture) : IAsyncLifetime
{
    private readonly RedisFixture _redisFixture = redisFixture;
    private PostgresFixture? _postgresFixture;
    private ApiWebApplicationFactory? _factory;

    protected HttpClient Client { get; private set; } = null!;
    protected IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The test factory has not been initialized.");

    public async Task InitializeAsync()
    {
        _postgresFixture = new PostgresFixture();
        await _postgresFixture.InitializeAsync();

        var redisScope = _redisFixture.CreateScope();
        _factory = new ApiWebApplicationFactory(_postgresFixture, _redisFixture, redisScope.KeyPrefix);
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgresFixture is not null)
        {
            await _postgresFixture.DisposeAsync();
        }
    }

    protected async Task AuthenticateAsAdminAsync(CancellationToken cancellationToken = default)
    {
        var token = await TestJwtFactory.CreateAdminTokenAsync(Services, cancellationToken: cancellationToken);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected async Task<T> WithDbContextAsync<T>(Func<RentACarDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        return await action(dbContext);
    }
}
