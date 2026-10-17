using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RentACar.API.Attributes;
using RentACar.API.Middleware;
using StackExchange.Redis;
using Xunit;

namespace RentACar.Tests.Unit.Middleware;

public sealed class IdempotencyResponseVersionTests
{
    [Fact]
    public async Task InvokeAsync_ResponseVersion_IgnoresUnversionedCacheEnvelopeAtMatchingKey()
    {
        var database = new Mock<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var privateResponse = JsonSerializer.Serialize(new
        {
            StatusCode = 200,
            ContentType = "application/json",
            Body = "{\"customerPhone\":\"PRIVATE\"}"
        });
        database.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)privateResponse);
        var calls = 0;
        var middleware = new IdempotencyMiddleware(context =>
        {
            calls++;
            return context.Response.WriteAsync("{\"publicCode\":\"NEW\"}");
        }, Mock.Of<ILogger<IdempotencyMiddleware>>());
        var context = Context(Guid.NewGuid().ToString(), "public-create-v1");

        await middleware.InvokeAsync(context, redis.Object);

        calls.Should().Be(1);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync()).Should().NotContain("PRIVATE");
    }

    [Theory]
    [InlineData("")]
    [InlineData("previous-response")]
    public async Task InvokeAsync_ChangedResponseVersion_IgnoresPreviouslyCachedPrivateBody(string previousVersion)
    {
        var database = new Mock<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var privateResponse = JsonSerializer.Serialize(new
        {
            StatusCode = 200,
            ContentType = "application/json",
            Body = "{\"customerPhone\":\"PRIVATE\"}"
        });
        string? previousKey = null;
        database.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
            {
                if (previousKey is null)
                {
                    previousKey = key.ToString();
                    return RedisValue.Null;
                }
                return key.ToString() == previousKey ? (RedisValue)privateResponse : RedisValue.Null;
            });
        var calls = 0;
        var middleware = new IdempotencyMiddleware(context =>
        {
            calls++;
            return context.Response.WriteAsync("{\"publicCode\":\"NEW\"}");
        }, Mock.Of<ILogger<IdempotencyMiddleware>>());
        var key = Guid.NewGuid().ToString();
        await middleware.InvokeAsync(Context(key, previousVersion), redis.Object);
        var updated = Context(key, "public-create-v1");

        await middleware.InvokeAsync(updated, redis.Object);

        calls.Should().Be(2);
        updated.Response.Headers["X-Idempotency-Status"].Should().Contain("MISS");
        updated.Response.Body.Position = 0;
        using var reader = new StreamReader(updated.Response.Body);
        (await reader.ReadToEndAsync()).Should().NotContain("PRIVATE");
    }

    [Theory]
    [InlineData("{\"quoteId\":\"6cf392af-a7b8-4267-a0d7-afd60b8985c5\"}")]
    [InlineData("{\"vehicleId\":\"6cf392af-a7b8-4267-a0d7-afd60b8985c5\"}")]
    public async Task InvokeAsync_ResponseVersion_PreservesIncomingKeyForServiceReplay(string body)
    {
        var key = Guid.NewGuid().ToString();
        var context = Context(key, "public-create-v1", body);
        var redis = new Mock<IConnectionMultiplexer>();
        string? observedKey = null;
        var middleware = new IdempotencyMiddleware(requestContext =>
        {
            observedKey = requestContext.Request.Headers["Idempotency-Key"].ToString();
            return Task.CompletedTask;
        }, Mock.Of<ILogger<IdempotencyMiddleware>>());

        await middleware.InvokeAsync(context, redis.Object);

        observedKey.Should().Be(key);
        context.Request.Body.Position.Should().Be(0);
        redis.Verify(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }

    private static DefaultHttpContext Context(string key, string version, string body = "{}")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/reservations";
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new IdempotentAttribute { ResponseVersion = version, UseQuoteReplay = true }), "reservation-create"));
        return context;
    }
}
