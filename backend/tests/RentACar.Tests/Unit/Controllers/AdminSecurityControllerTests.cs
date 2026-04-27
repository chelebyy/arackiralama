using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Authentication;
using RentACar.API.Controllers;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminSecurityControllerTests
{
    [Fact]
    public void Ping_WithAdminRole_ReturnsOkWithAuthorizedStatus()
    {
        var controller = CreateController(role: AuthRoleNames.Admin);

        var result = controller.Ping();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<object>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        json.Should().Contain("\"status\":\"authorized\"");
        json.Should().Contain("\"role\":\"Admin\"");
        json.Should().Contain("\"utcTime\"");
    }

    [Fact]
    public void Ping_WithoutRoleClaim_ReturnsOkWithUnknownRole()
    {
        var controller = CreateController(role: null);

        var result = controller.Ping();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<object>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        json.Should().Contain("\"role\":\"unknown\"");
    }

    private static AdminSecurityController CreateController(string? role)
    {
        var claims = new List<Claim>();
        if (role != null)
        {
            claims.Add(new Claim(AuthClaimTypes.Role, role));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        return new AdminSecurityController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }
}
