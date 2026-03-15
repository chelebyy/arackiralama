using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using RentACar.API.Authentication;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Auth;
using RentACar.API.Controllers;
using RentACar.API.Options;
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
    public async Task Login_WithNormalizedEmailAndValidCredentials_ReturnsOkAndResetsLockoutState()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(
            passwordHasher,
            "Admin@Test.com",
            failedLoginCount: 4,
            lockoutEndUtc: DateTime.UtcNow.AddMinutes(-1));

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

        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: refreshTokenCookieService.Object);

        var result = await controller.Login(new AdminLoginRequest("  ADMIN@test.com  ", "P@ssw0rd!"), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminLoginResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AccessToken.Should().Be("jwt-token");
        response.Data.Email.Should().Be(adminUser.Email);

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.FailedLoginCount.Should().Be(0);
        persistedAdmin.LockoutEndUtc.Should().BeNull();
        persistedAdmin.LastLoginAtUtc.Should().NotBeNull();

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
    public async Task Login_WithInvalidPassword_TracksFailuresAndLocksOnFifthAttempt()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.AdminUsers.Add(CreateAdminUser(passwordHasher, "admin@test.com"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var result = await controller.Login(new AdminLoginRequest("admin@test.com", "wrong-password"), CancellationToken.None);
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.FailedLoginCount.Should().Be(5);
        persistedAdmin.LockoutEndUtc.Should().NotBeNull();
        persistedAdmin.LockoutEndUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
        dbContext.AuthSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_WhenLockoutIsActive_ReturnsUnauthorizedAndDoesNotIssueSession()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();

        dbContext.AdminUsers.Add(CreateAdminUser(
            passwordHasher,
            "admin@test.com",
            failedLoginCount: 5,
            lockoutEndUtc: DateTime.UtcNow.AddMinutes(10)));

        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: Mock.Of<IRefreshTokenCookieService>());

        var result = await controller.Login(new AdminLoginRequest("admin@test.com", "P@ssw0rd!"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        dbContext.AuthSessions.Should().BeEmpty();
        jwtTokenService.Verify(service => service.HashRefreshToken(It.IsAny<string>()), Times.Never);
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
    public async Task Refresh_WithActiveSession_RotatesSessionAndIssuesNewTokens()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(passwordHasher, "refresh-admin@test.com");
        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync();

        var currentSessionId = Guid.NewGuid();
        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = currentSessionId,
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = "sha256:incoming-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-3)
        });
        await dbContext.SaveChangesAsync();

        var expectedAccessExpiry = DateTime.UtcNow.AddMinutes(15);
        var expectedRefreshExpiry = DateTime.UtcNow.AddDays(7);
        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("incoming-refresh-token"))
            .Returns("sha256:incoming-refresh-hash");
        jwtTokenService
            .Setup(service => service.CreateAdminAccessToken(It.IsAny<AdminUser>(), It.IsAny<Guid>(), out expectedAccessExpiry))
            .Returns("rotated-admin-access-token");
        jwtTokenService
            .Setup(service => service.CreateRefreshToken(out expectedRefreshExpiry))
            .Returns("rotated-admin-refresh-token");
        jwtTokenService
            .Setup(service => service.HashRefreshToken("rotated-admin-refresh-token"))
            .Returns("sha256:rotated-admin-refresh-hash");

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: refreshTokenCookieService.Object);

        SetRefreshCookie(controller.HttpContext, "__Host-rac_refresh", "incoming-refresh-token");

        var result = await controller.Refresh(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminRefreshResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AccessToken.Should().Be("rotated-admin-access-token");

        dbContext.AuthSessions.Should().HaveCount(2);
        var revokedSession = dbContext.AuthSessions.Single(session => session.Id == currentSessionId);
        revokedSession.RevokedAtUtc.Should().NotBeNull();
        revokedSession.ReplacedBySessionId.Should().NotBeNull();

        var replacementSession = dbContext.AuthSessions.Single(session => session.Id == revokedSession.ReplacedBySessionId);
        replacementSession.PrincipalType.Should().Be(AuthPrincipalType.Admin);
        replacementSession.PrincipalId.Should().Be(adminUser.Id);
        replacementSession.RefreshTokenHash.Should().Be("sha256:rotated-admin-refresh-hash");
        replacementSession.RefreshTokenExpiresAtUtc.Should().Be(expectedRefreshExpiry);

        refreshTokenCookieService.Verify(
            service => service.AppendRefreshTokenCookie(
                It.IsAny<HttpContext>(),
                "rotated-admin-refresh-token",
                expectedRefreshExpiry),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WithReplayedRefreshToken_ReturnsUnauthorizedGenericPayload()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(passwordHasher, "replay-admin@test.com");
        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync();

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = "sha256:replayed-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            ReplacedBySessionId = Guid.NewGuid(),
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-2)
        });
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("replayed-refresh-token"))
            .Returns("sha256:replayed-refresh-hash");

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: refreshTokenCookieService.Object);

        SetRefreshCookie(controller.HttpContext, "__Host-rac_refresh", "replayed-refresh-token");

        var result = await controller.Refresh(CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Yetkisiz erişim");

        dbContext.AuthSessions.Should().HaveCount(1);
        jwtTokenService.Verify(service => service.CreateRefreshToken(out It.Ref<DateTime>.IsAny), Times.Never);
        refreshTokenCookieService.Verify(
            service => service.AppendRefreshTokenCookie(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task Refresh_WithExpiredSession_ReturnsUnauthorizedGenericPayload()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(passwordHasher, "expired-admin@test.com");
        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync();

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = "sha256:expired-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-8)
        });
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("expired-refresh-token"))
            .Returns("sha256:expired-refresh-hash");

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: refreshTokenCookieService.Object);

        SetRefreshCookie(controller.HttpContext, "__Host-rac_refresh", "expired-refresh-token");

        var result = await controller.Refresh(CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Yetkisiz erişim");

        dbContext.AuthSessions.Should().HaveCount(1);
        refreshTokenCookieService.Verify(
            service => service.AppendRefreshTokenCookie(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task Logout_WithValidSession_RevokesSessionAndClearsRefreshCookie()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(passwordHasher, "logout-admin@test.com");
        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = sessionId,
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = "sha256:logout-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            LastSeenAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(dbContext, refreshTokenCookieService: refreshTokenCookieService.Object);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Admin),
                new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, adminUser.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, sessionId.ToString()),
                new Claim(AuthClaimTypes.PrincipalType, AuthPrincipalType.Admin.ToString())
            ],
            authenticationType: "Bearer"));

        var result = await controller.Logout(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        payloadJson.Should().Contain("\"Success\":true");

        var session = dbContext.AuthSessions.Should().ContainSingle().Subject;
        session.RevokedAtUtc.Should().NotBeNull();

        refreshTokenCookieService.Verify(
            service => service.ClearRefreshTokenCookie(It.IsAny<HttpContext>()),
            Times.Once);
    }

    [Fact]
    public async Task Logout_WithMissingSessionClaims_ReturnsUnauthorizedAndClearsCookie()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(dbContext, refreshTokenCookieService: refreshTokenCookieService.Object);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.Logout(CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Yetkisiz erişim");

        refreshTokenCookieService.Verify(service => service.ClearRefreshTokenCookie(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Logout_WithSessionMismatch_ReturnsUnauthorizedAndClearsCookie()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var adminUser = CreateAdminUser(passwordHasher, "mismatch-admin@test.com");
        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync();

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = Guid.NewGuid(),
            RefreshTokenHash = "sha256:mismatch-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(2)
        });
        await dbContext.SaveChangesAsync();

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(dbContext, refreshTokenCookieService: refreshTokenCookieService.Object);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, adminUser.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString())
            ],
            authenticationType: "Bearer"));

        var result = await controller.Logout(CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Yetkisiz erişim");

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.RevokedAtUtc.Should().BeNull();
        refreshTokenCookieService.Verify(service => service.ClearRefreshTokenCookie(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public void Me_WithAdminPrincipal_ReturnsIdentityPayload()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "admin-id"),
                        new Claim(ClaimTypes.Name, "admin@test.com"),
                        new Claim(ClaimTypes.Role, AuthRoleNames.Admin)
                    ],
                    authenticationType: "TestAuth"))
            }
        };

        var result = controller.Me();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        payloadJson.Should().Contain("\"Success\":true");
        payloadJson.Should().Contain("\"id\":\"admin-id\"");
        payloadJson.Should().Contain("\"email\":\"admin@test.com\"");
        payloadJson.Should().Contain($"\"role\":\"{AuthRoleNames.Admin}\"");
    }

    private static AdminAuthController CreateController(
        IApplicationDbContext dbContext,
        IPasswordHasher? passwordHasher = null,
        IJwtTokenService? jwtTokenService = null,
        IRefreshTokenCookieService? refreshTokenCookieService = null,
        RefreshTokenCookieSettings? refreshTokenCookieSettings = null)
    {
        passwordHasher ??= new Mock<IPasswordHasher>().Object;
        jwtTokenService ??= Mock.Of<IJwtTokenService>();
        refreshTokenCookieService ??= Mock.Of<IRefreshTokenCookieService>();
        refreshTokenCookieSettings ??= new RefreshTokenCookieSettings();

        return new AdminAuthController(
            dbContext,
            passwordHasher,
            jwtTokenService,
            refreshTokenCookieService,
            Options.Create(refreshTokenCookieSettings))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static void SetRefreshCookie(HttpContext httpContext, string cookieName, string refreshToken)
    {
        httpContext.Request.Headers.Cookie = $"{cookieName}={refreshToken}";
    }

    private static AdminUser CreateAdminUser(
        IPasswordHasher passwordHasher,
        string email,
        bool isActive = true,
        int failedLoginCount = 0,
        DateTime? lockoutEndUtc = null)
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword("P@ssw0rd!"),
            FullName = "Test Admin",
            Role = AuthRoleNames.Admin,
            IsActive = isActive,
            FailedLoginCount = failedLoginCount,
            LockoutEndUtc = lockoutEndUtc
        };
    }
}
