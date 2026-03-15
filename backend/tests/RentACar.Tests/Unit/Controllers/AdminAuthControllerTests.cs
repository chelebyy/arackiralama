using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Authentication;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Auth;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Security;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public class AdminAuthControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminAuthControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task Login_WithMissingCredentials_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.Login(new AdminLoginRequest(string.Empty, string.Empty), CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Email ve parola zorunludur.");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(passwordHasher, "admin@test.com");
        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync();

        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);
        var expectedRefreshExpiry = DateTime.UtcNow.AddDays(7);
        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.CreateAdminAccessToken(It.IsAny<AdminUser>(), It.IsAny<Guid>(), out expectedExpiry))
            .Returns("jwt-token");
        jwtTokenService
            .Setup(service => service.CreateRefreshToken(out expectedRefreshExpiry))
            .Returns("refresh-token");
        jwtTokenService
            .Setup(service => service.HashRefreshToken("refresh-token"))
            .Returns("sha256:refresh-token-hash");

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();

        var controller = new AdminAuthController(dbContext, passwordHasher, jwtTokenService.Object, refreshTokenCookieService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = await controller.Login(new AdminLoginRequest("  admin@test.com  ", "P@ssw0rd!"), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminLoginResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AccessToken.Should().Be("jwt-token");
        response.Data.Email.Should().Be(adminUser.Email);

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.PrincipalType.Should().Be(AuthPrincipalType.Admin);
        persistedSession.PrincipalId.Should().Be(adminUser.Id);
        persistedSession.RefreshTokenHash.Should().Be("sha256:refresh-token-hash");
        persistedSession.RefreshTokenExpiresAtUtc.Should().Be(expectedRefreshExpiry);

        jwtTokenService.Verify(
            service => service.CreateAdminAccessToken(
                It.Is<AdminUser>(user => user.Id == adminUser.Id),
                It.Is<Guid>(sessionId => sessionId == persistedSession.Id),
                out expectedExpiry),
            Times.Once);
        jwtTokenService.Verify(service => service.CreateRefreshToken(out expectedRefreshExpiry), Times.Once);
        jwtTokenService.Verify(service => service.HashRefreshToken("refresh-token"), Times.Once);
        refreshTokenCookieService.Verify(
            service => service.AppendRefreshTokenCookie(
                It.IsAny<HttpContext>(),
                "refresh-token",
                expectedRefreshExpiry),
            Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.AdminUsers.Add(CreateAdminUser(passwordHasher, "admin@test.com"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);
        var result = await controller.Login(new AdminLoginRequest("admin@test.com", "wrong-password"), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Yetkisiz erişim");
    }

    [Fact]
    public async Task Login_WithCaseMismatchedEmail_ReturnsUnauthorized()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.AdminUsers.Add(CreateAdminUser(passwordHasher, "Admin@Test.com"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);
        var result = await controller.Login(new AdminLoginRequest("admin@test.com", "P@ssw0rd!"), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.AdminUsers.Add(CreateAdminUser(passwordHasher, "admin@test.com", isActive: false));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);
        var result = await controller.Login(new AdminLoginRequest("admin@test.com", "P@ssw0rd!"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.AdminUsers.Add(CreateAdminUser(passwordHasher, "admin@test.com"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);
        var result = await controller.Login(new AdminLoginRequest("admin@test.com", "wrong-password"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void Logout_ClearsRefreshTokenCookie()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(dbContext, refreshTokenCookieService: refreshTokenCookieService.Object);

        var result = controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        refreshTokenCookieService.Verify(service => service.ClearRefreshTokenCookie(It.IsAny<HttpContext>()), Times.Once);
    }

    private static AdminAuthController CreateController(
        IApplicationDbContext dbContext,
        IPasswordHasher? passwordHasher = null,
        IJwtTokenService? jwtTokenService = null,
        IRefreshTokenCookieService? refreshTokenCookieService = null)
    {
        passwordHasher ??= new Mock<IPasswordHasher>().Object;
        jwtTokenService ??= Mock.Of<IJwtTokenService>();
        refreshTokenCookieService ??= Mock.Of<IRefreshTokenCookieService>();

        return new AdminAuthController(dbContext, passwordHasher, jwtTokenService, refreshTokenCookieService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AdminUser CreateAdminUser(IPasswordHasher passwordHasher, string email, bool isActive = true)
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword("P@ssw0rd!"),
            FullName = "Test Admin",
            Role = "Admin",
            IsActive = isActive
        };
    }
}
