using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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

    [Fact]
    public async Task GetHealth_WhenNotDevelopment_ReturnsSecurityHeaders()
    {
        using var httpsClient = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await httpsClient.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();
        response.Headers.GetValues("Permissions-Policy").Should().ContainSingle("camera=(), geolocation=(), microphone=()");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("no-referrer");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle();
    }

    [Fact]
    public async Task OpenApiEndpoints_AreHiddenOutsideDevelopment()
    {
        var openApiResponse = await _client!.GetAsync("/openapi/v1.json");
        var swaggerResponse = await _client.GetAsync("/swagger/index.html");

        openApiResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        swaggerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorsPreflight_WhenOriginAllowed_ReturnsCorsHeaders()
    {
        var redisScope = _redisFixture.CreateScope();
        await using var factory = new ApiWebApplicationFactory(
            _postgresFixture!,
            _redisFixture,
            redisScope.KeyPrefix,
            new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://app.local.test"
            });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "https://app.local.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle("https://app.local.test");
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle("true");
    }
}
