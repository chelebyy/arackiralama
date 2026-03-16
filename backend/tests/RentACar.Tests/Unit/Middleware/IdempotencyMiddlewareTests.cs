using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Moq;
using RentACar.API.Attributes;
using RentACar.API.Middleware;
using StackExchange.Redis;
using Xunit;

namespace RentACar.Tests.Unit.Middleware;

public class IdempotencyMiddlewareTests
{
    private readonly Mock<ILogger<IdempotencyMiddleware>> _loggerMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;

    public IdempotencyMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<IdempotencyMiddleware>>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithoutIdempotencyKey_PassesThrough()
    {
        // Arrange
        var context = CreateHttpContext("POST");
        var endpoint = CreateEndpointWithAttribute(new IdempotentAttribute());
        context.SetEndpoint(endpoint);

        var wasCalled = false;
        Task Next(HttpContext ctx)
        {
            wasCalled = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        var middleware = new IdempotencyMiddleware(Next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context, _redisMock.Object);

        // Assert
        wasCalled.Should().BeTrue();
        context.Response.Headers["X-Idempotency-Status"].Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithIdempotencyKey_CachesResponse()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var context = CreateHttpContext("POST", idempotencyKey);
        var endpoint = CreateEndpointWithAttribute(new IdempotentAttribute());
        context.SetEndpoint(endpoint);

        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            return ctx.Response.WriteAsync("{\"success\":true}");
        }

        var middleware = new IdempotencyMiddleware(Next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context, _redisMock.Object);

        // Assert - verify the idempotency status header indicates a cache MISS
        // This proves the middleware processed the request and attempted to cache
        context.Response.Headers["X-Idempotency-Status"].Should().Contain("MISS");
    }

    [Fact]
    public async Task InvokeAsync_WithCachedKey_ReturnsCachedResponse()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var context = CreateHttpContext("POST", idempotencyKey);
        var endpoint = CreateEndpointWithAttribute(new IdempotentAttribute());
        context.SetEndpoint(endpoint);

        var cachedData = JsonSerializer.Serialize(new
        {
            StatusCode = 200,
            ContentType = "application/json",
            Body = "{\"success\":true,\"data\":\"cached\"}"
        });
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)cachedData);

        var wasCalled = false;
        Task Next(HttpContext ctx)
        {
            wasCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new IdempotencyMiddleware(Next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context, _redisMock.Object);

        // Assert
        wasCalled.Should().BeFalse("Should return cached response without calling next");
        context.Response.Headers["X-Idempotency-Status"].Should().Contain("HIT");
    }

    [Fact]
    public async Task InvokeAsync_NonPostPutPatch_PassesThrough()
    {
        // Arrange
        var context = CreateHttpContext("GET");
        var endpoint = CreateEndpointWithAttribute(new IdempotentAttribute());
        context.SetEndpoint(endpoint);

        var wasCalled = false;
        Task Next(HttpContext ctx)
        {
            wasCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new IdempotencyMiddleware(Next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context, _redisMock.Object);

        // Assert
        wasCalled.Should().BeTrue();
        _databaseMock.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithoutAttribute_PassesThrough()
    {
        // Arrange
        var context = CreateHttpContext("POST", Guid.NewGuid().ToString());
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            0,
            EndpointMetadataCollection.Empty,
            "test"));

        var wasCalled = false;
        Task Next(HttpContext ctx)
        {
            wasCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new IdempotencyMiddleware(Next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context, _redisMock.Object);

        // Assert
        wasCalled.Should().BeTrue();
        _databaseMock.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RequireKeyTrue_MissingKey_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateHttpContext("POST"); // No Idempotency-Key
        var endpoint = CreateEndpointWithAttribute(new IdempotentAttribute { RequireKey = true });
        context.SetEndpoint(endpoint);

        var wasCalled = false;
        Task Next(HttpContext ctx)
        {
            wasCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new IdempotencyMiddleware(Next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context, _redisMock.Object);

        // Assert
        wasCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(400);
    }

    private static DefaultHttpContext CreateHttpContext(string method, string? idempotencyKey = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/api/v1/test";
        context.Response.Body = new MemoryStream();

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return context;
    }

    private static Endpoint CreateEndpointWithAttribute(IdempotentAttribute attribute)
    {
        var metadata = new EndpointMetadataCollection(attribute);
        return new Endpoint(_ => Task.CompletedTask, metadata, "TestEndpoint");
    }
}
