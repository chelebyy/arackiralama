using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RentACar.API.Middleware;
using Xunit;

namespace RentACar.Tests.Unit.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenMethodAndPathContainNewLines_LogsSanitizedValues()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();

        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "GE\r\nT";
        context.Request.Path = "/cars\r\nforged";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("GET /carsforged -> 200", StringComparison.Ordinal)
                    && !state.ToString()!.Contains("\r", StringComparison.Ordinal)
                    && !state.ToString()!.Contains("\n", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
