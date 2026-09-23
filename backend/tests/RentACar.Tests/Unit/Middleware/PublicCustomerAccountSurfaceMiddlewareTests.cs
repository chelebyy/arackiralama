using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RentACar.API.Middleware;
using Xunit;

namespace RentACar.Tests.Unit.Middleware;

public sealed class PublicCustomerAccountSurfaceMiddlewareTests
{
    [Theory]
    [InlineData("POST", "/api/customer/v1/auth/register")]
    [InlineData("POST", "/api/customer/v1/auth/claim")]
    [InlineData("POST", "/api/customer/v1/auth/register/")]
    [InlineData("POST", "/api/customer/v1/auth/claim/")]
    [InlineData("POST", "/API/CUSTOMER/V1/AUTH/REGISTER")]
    [InlineData("GET", "/api/customer/v1/auth/register")]
    public async Task InvokeAsync_WhenPublicAccountSurfaceIsRequested_ReturnsNotFound(
        string method,
        string path)
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new PublicCustomerAccountSurfaceMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenCustomerLoginIsRequested_ContinuesPipeline()
    {
        var nextCalled = false;
        RequestDelegate next = context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };
        var middleware = new PublicCustomerAccountSurfaceMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/customer/v1/auth/login";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        nextCalled.Should().BeTrue();
    }
}
