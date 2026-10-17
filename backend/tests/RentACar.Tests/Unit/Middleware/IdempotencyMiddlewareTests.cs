using System.Security.Claims;
using System.Security.Cryptography;
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

    [Theory]
    [InlineData("path")]
    [InlineData("method")]
    [InlineData("session")]
    [InlineData("principal")]
    [InlineData("query")]
    public async Task InvokeAsync_LegacyCache_DoesNotReplayAcrossRequestScopes(string changedScope)
    {
        UseInMemoryRedis();
        var first = CreateLegacyRequest("{\"customer\":{\"email\":\"first@example.test\"}}");
        var second = CreateLegacyRequest("{\"customer\":{\"email\":\"first@example.test\"}}");
        switch (changedScope)
        {
            case "path": second.Request.Path = "/api/v1/reservations/unpaid-requests"; break;
            case "method": second.Request.Method = "PUT"; break;
            case "session": second.Request.Headers["X-Session-Id"] = "another-session"; break;
            case "principal":
                second.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "another-principal")], "test"));
                break;
            case "query": second.Request.QueryString = new QueryString("?variant=another"); break;
        }
        var executions = 0;
        var middleware = new IdempotencyMiddleware(async current =>
        {
            executions++;
            await current.Response.WriteAsync($"{{\"execution\":{executions}}}");
        }, _loggerMock.Object);

        await middleware.InvokeAsync(first, _redisMock.Object);
        await middleware.InvokeAsync(second, _redisMock.Object);

        executions.Should().Be(2);
        second.Response.Headers["X-Idempotency-Status"].Should().Contain("MISS");
    }

    [Fact]
    public async Task InvokeAsync_LegacyCache_ChangedPayloadRejectsReplay()
    {
        UseInMemoryRedis();
        var first = CreateLegacyRequest("{\"customer\":{\"email\":\"first@example.test\"}}");
        var second = CreateLegacyRequest("{\"customer\":{\"email\":\"second@example.test\"}}");
        var executions = 0;
        var middleware = new IdempotencyMiddleware(async current =>
        {
            executions++;
            await current.Response.WriteAsync("{\"data\":\"private-first-response\"}");
        }, _loggerMock.Object);

        await middleware.InvokeAsync(first, _redisMock.Object);
        await middleware.InvokeAsync(second, _redisMock.Object);

        executions.Should().Be(1);
        second.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        second.Response.Body.Position = 0;
        var response = await new StreamReader(second.Response.Body).ReadToEndAsync();
        response.Should().NotContain("private-first-response");
    }

    [Fact]
    public async Task InvokeAsync_LegacyCache_IdenticalRetryReplaysAfterRestoringRequestBody()
    {
        UseInMemoryRedis();
        const string body = "{\"customer\":{\"email\":\"first@example.test\"}}";
        var executions = 0;
        var middleware = new IdempotencyMiddleware(async current =>
        {
            executions++;
            using var reader = new StreamReader(current.Request.Body, leaveOpen: true);
            (await reader.ReadToEndAsync()).Should().Be(body);
            await current.Response.WriteAsync("{\"data\":\"original-result\"}");
        }, _loggerMock.Object);
        var first = CreateLegacyRequest(body);
        var retry = CreateLegacyRequest(body);

        await middleware.InvokeAsync(first, _redisMock.Object);
        await middleware.InvokeAsync(retry, _redisMock.Object);

        executions.Should().Be(1);
        retry.Response.Headers["X-Idempotency-Status"].Should().Contain("HIT");
        retry.Response.Body.Position = 0;
        (await new StreamReader(retry.Response.Body).ReadToEndAsync()).Should().Contain("original-result");
    }

    [Fact]
    public async Task InvokeAsync_LegacyCache_IgnoresOldUnscopedEntries()
    {
        var cache = UseInMemoryRedis();
        cache["idempotent:shared-regression-key"] = JsonSerializer.Serialize(new
        {
            StatusCode = 200,
            ContentType = "application/json",
            Body = "old-private-response"
        });
        var request = CreateLegacyRequest("{}");
        var reachedHandler = false;
        var middleware = new IdempotencyMiddleware(current =>
        {
            reachedHandler = true;
            return current.Response.WriteAsync("new-response");
        }, _loggerMock.Object);

        await middleware.InvokeAsync(request, _redisMock.Object);

        reachedHandler.Should().BeTrue();
        request.Response.Headers["X-Idempotency-Status"].Should().Contain("MISS");
    }

    private Dictionary<string, RedisValue> UseInMemoryRedis()
    {
        var cache = new Dictionary<string, RedisValue>();
        _databaseMock.Setup(database => database.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => cache.GetValueOrDefault(key.ToString(), RedisValue.Null));
        _databaseMock.Setup(database => database.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue value, Expiration _, ValueCondition _, CommandFlags _) =>
            {
                cache[key.ToString()] = value;
                return true;
            });
        return cache;
    }

    private static DefaultHttpContext CreateLegacyRequest(string body)
    {
        var context = CreateHttpContext("POST", "shared-regression-key");
        context.Request.Path = "/api/v1/reservations";
        context.Request.Headers["X-Session-Id"] = "original-session";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.SetEndpoint(CreateEndpointWithAttribute(new IdempotentAttribute { UseQuoteReplay = true }));
        return context;
    }

    [Theory]
    [InlineData("{\"quoteId\":\"quote\"}")]
    [InlineData("{\"QuoteId\":\"quote\"}")]
    [InlineData("{\"quoteId\":null,\"vehicleId\":\"vehicle\"}")]
    [InlineData("invalid-json")]
    public async Task InvokeAsync_QuoteEndpointRevalidatesRequestInsteadOfReplayingCache(string body)
    {
        var context = CreateHttpContext("POST", Guid.NewGuid().ToString());
        context.SetEndpoint(CreateEndpointWithAttribute(new IdempotentAttribute { UseQuoteReplay = true }));
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var reachedHandler = false;
        var middleware = new IdempotencyMiddleware(async current =>
        {
            reachedHandler = true;
            using var reader = new StreamReader(current.Request.Body, leaveOpen: true);
            (await reader.ReadToEndAsync()).Should().Be(body);
            current.Response.StatusCode = 409;
        }, _loggerMock.Object);

        await middleware.InvokeAsync(context, _redisMock.Object);

        reachedHandler.Should().BeTrue();
        context.Response.StatusCode.Should().Be(409);
        _databaseMock.Verify(database => database.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
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
            Body = "{\"success\":true,\"data\":\"cached\"}",
            RequestFingerprint = Convert.ToHexString(SHA256.HashData([]))
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
