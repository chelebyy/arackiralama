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

public class CustomerAuthControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public CustomerAuthControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task Register_WithNewEmail_CreatesCustomerWithHashedPassword()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var controller = CreateController(dbContext, passwordHasher: passwordHasher);

        var result = await controller.Register(
            new CustomerRegisterRequest("  Customer@Test.com  ", "P@ssw0rd!", " Test Customer ", " 05001112233 "),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var customer = dbContext.Customers.Should().ContainSingle().Subject;
        customer.Email.Should().Be("Customer@Test.com");
        customer.NormalizedEmail.Should().Be("CUSTOMER@TEST.COM");
        customer.PasswordHash.Should().NotBeNullOrWhiteSpace();
        customer.PasswordHash.Should().NotBe("P@ssw0rd!");
        passwordHasher.VerifyPassword("P@ssw0rd!", customer.PasswordHash!).Should().BeTrue();
        customer.FullName.Should().Be("Test Customer");
        customer.Phone.Should().Be("05001112233");
    }

    [Fact]
    public async Task Register_WhenGuestCustomerExists_UpgradesExistingCustomer()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var existingCustomer = new Customer
        {
            Email = "guest@test.com",
            FullName = "Guest Person",
            Phone = "05000000000",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = null
        };

        dbContext.Customers.Add(existingCustomer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);

        var result = await controller.Register(
            new CustomerRegisterRequest("GUEST@Test.com", "Secure123!", "Registered Person", "05009998877"),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        dbContext.Customers.Should().HaveCount(1);
        var upgradedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        upgradedCustomer.Id.Should().Be(existingCustomer.Id);
        upgradedCustomer.HasPassword.Should().BeTrue();
        passwordHasher.VerifyPassword("Secure123!", upgradedCustomer.PasswordHash!).Should().BeTrue();
        upgradedCustomer.FullName.Should().Be("Registered Person");
        upgradedCustomer.Phone.Should().Be("05009998877");
    }

    [Fact]
    public async Task Register_WhenCustomerAlreadyRegistered_ReturnsSuccessWithoutLeaking()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var existingHash = passwordHasher.HashPassword("Existing123!");
        dbContext.Customers.Add(new Customer
        {
            Email = "existing@test.com",
            FullName = "Existing",
            Phone = "05000000000",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = existingHash
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);

        var result = await controller.Register(
            new CustomerRegisterRequest("EXISTING@test.com", "NewPass123!", null, null),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        response.Should().NotBeNull();
        ReadBoolProperty(response, nameof(ApiResponse<object>.Success)).Should().BeTrue();
        ReadStringProperty(response, nameof(ApiResponse<object>.Message)).Should().Be("Kayit basarili.");
        ReadProperty(response, nameof(ApiResponse<object>.Data)).Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithMissingCredentials_ReturnsUnauthorizedGenericPayload()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.Login(new CustomerLoginRequest(string.Empty, string.Empty), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Yetkisiz erişim");
    }

    [Fact]
    public async Task Login_WhenCustomerHasNoPassword_ReturnsUnauthorizedWithoutMutation()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "guest@test.com",
            FullName = "Guest",
            Phone = "05001112233",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = null,
            FailedLoginCount = 0
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: new BcryptPasswordHasher());

        var result = await controller.Login(new CustomerLoginRequest("guest@test.com", "AnyPassword1!"), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Which.Message.Should().Be("Yetkisiz erişim");

        var persistedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        persistedCustomer.FailedLoginCount.Should().Be(0);
        persistedCustomer.LockoutEndUtc.Should().BeNull();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_TracksFailuresAndLocksOnFifthAttempt()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.Customers.Add(new Customer
        {
            Email = "locked@test.com",
            FullName = "Locked User",
            Phone = "05001110000",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = passwordHasher.HashPassword("CorrectPass1!")
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, passwordHasher: passwordHasher);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var result = await controller.Login(new CustomerLoginRequest("locked@test.com", "WrongPass!"), CancellationToken.None);
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        var persistedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        persistedCustomer.FailedLoginCount.Should().Be(5);
        persistedCustomer.LockoutEndUtc.Should().NotBeNull();
        persistedCustomer.LockoutEndUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task Login_WhenLockoutIsActive_ReturnsUnauthorizedAndDoesNotIssueSession()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        dbContext.Customers.Add(new Customer
        {
            Email = "active-lockout@test.com",
            FullName = "Locked User",
            Phone = "05001110000",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = passwordHasher.HashPassword("CorrectPass1!"),
            FailedLoginCount = 5,
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(10)
        });
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: Mock.Of<IRefreshTokenCookieService>());

        var result = await controller.Login(new CustomerLoginRequest("active-lockout@test.com", "CorrectPass1!"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        dbContext.AuthSessions.Should().BeEmpty();
        jwtTokenService.Verify(service => service.HashRefreshToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ResetsLockoutAndPersistsCustomerSession()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var customer = new Customer
        {
            Email = "customer@login.test",
            FullName = "Auth Customer",
            Phone = "05001114455",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = passwordHasher.HashPassword("P@ssw0rd!"),
            FailedLoginCount = 4,
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(-2)
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);
        var expectedRefreshExpiry = DateTime.UtcNow.AddDays(7);
        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.CreateCustomerAccessToken(It.IsAny<Customer>(), It.IsAny<Guid>(), out expectedExpiry))
            .Returns("customer-access-token");
        jwtTokenService
            .Setup(service => service.CreateRefreshToken(out expectedRefreshExpiry))
            .Returns("customer-refresh-token");
        jwtTokenService
            .Setup(service => service.HashRefreshToken("customer-refresh-token"))
            .Returns("sha256:customer-refresh-token-hash");

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();

        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: refreshTokenCookieService.Object);

        var result = await controller.Login(new CustomerLoginRequest("CUSTOMER@login.test", "P@ssw0rd!"), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CustomerAuthResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AccessToken.Should().Be("customer-access-token");
        response.Data.Email.Should().Be(customer.Email);

        var persistedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        persistedCustomer.FailedLoginCount.Should().Be(0);
        persistedCustomer.LockoutEndUtc.Should().BeNull();
        persistedCustomer.LastLoginAtUtc.Should().NotBeNull();

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.PrincipalType.Should().Be(AuthPrincipalType.Customer);
        persistedSession.PrincipalId.Should().Be(customer.Id);
        persistedSession.RefreshTokenHash.Should().Be("sha256:customer-refresh-token-hash");
        persistedSession.RefreshTokenExpiresAtUtc.Should().Be(expectedRefreshExpiry);

        jwtTokenService.Verify(
            service => service.CreateCustomerAccessToken(
                It.Is<Customer>(principal => principal.Id == customer.Id),
                It.Is<Guid>(sessionId => sessionId == persistedSession.Id),
                out expectedExpiry),
            Times.Once);
        jwtTokenService.Verify(service => service.CreateRefreshToken(out expectedRefreshExpiry), Times.Once);
        jwtTokenService.Verify(service => service.HashRefreshToken("customer-refresh-token"), Times.Once);
        refreshTokenCookieService.Verify(
            service => service.AppendRefreshTokenCookie(
                It.IsAny<HttpContext>(),
                "customer-refresh-token",
                expectedRefreshExpiry),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WithActiveSession_RotatesSessionAndIssuesNewTokens()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "refresh@test.com",
            FullName = "Refresh Customer",
            Phone = "05009990000",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = "hashed-password"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var currentSessionId = Guid.NewGuid();
        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = currentSessionId,
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
            RefreshTokenHash = "sha256:incoming-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        await dbContext.SaveChangesAsync();

        var expectedAccessExpiry = DateTime.UtcNow.AddMinutes(15);
        var expectedRefreshExpiry = DateTime.UtcNow.AddDays(7);
        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("incoming-refresh-token"))
            .Returns("sha256:incoming-refresh-hash");
        jwtTokenService
            .Setup(service => service.CreateCustomerAccessToken(It.IsAny<Customer>(), It.IsAny<Guid>(), out expectedAccessExpiry))
            .Returns("rotated-access-token");
        jwtTokenService
            .Setup(service => service.CreateRefreshToken(out expectedRefreshExpiry))
            .Returns("rotated-refresh-token");
        jwtTokenService
            .Setup(service => service.HashRefreshToken("rotated-refresh-token"))
            .Returns("sha256:rotated-refresh-hash");

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            refreshTokenCookieService: refreshTokenCookieService.Object);

        SetRefreshCookie(controller.HttpContext, "__Host-rac_refresh", "incoming-refresh-token");

        var result = await controller.Refresh(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CustomerRefreshResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.AccessToken.Should().Be("rotated-access-token");

        dbContext.AuthSessions.Should().HaveCount(2);
        var revokedSession = dbContext.AuthSessions.Single(session => session.Id == currentSessionId);
        revokedSession.RevokedAtUtc.Should().NotBeNull();
        revokedSession.ReplacedBySessionId.Should().NotBeNull();

        var replacementSession = dbContext.AuthSessions.Single(session => session.Id == revokedSession.ReplacedBySessionId);
        replacementSession.PrincipalType.Should().Be(AuthPrincipalType.Customer);
        replacementSession.PrincipalId.Should().Be(customer.Id);
        replacementSession.RefreshTokenHash.Should().Be("sha256:rotated-refresh-hash");
        replacementSession.RefreshTokenExpiresAtUtc.Should().Be(expectedRefreshExpiry);

        refreshTokenCookieService.Verify(
            service => service.AppendRefreshTokenCookie(
                It.IsAny<HttpContext>(),
                "rotated-refresh-token",
                expectedRefreshExpiry),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WithReplayedRefreshToken_ReturnsUnauthorizedGenericPayload()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "replay@test.com",
            FullName = "Replay Customer",
            Phone = "05008887766",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = "hashed-password"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var replacedBySessionId = Guid.NewGuid();
        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
            RefreshTokenHash = "sha256:replayed-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            ReplacedBySessionId = replacedBySessionId,
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
        var customer = new Customer
        {
            Email = "expired@test.com",
            FullName = "Expired Session Customer",
            Phone = "05007776655",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = "hashed-password"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
            RefreshTokenHash = "sha256:expired-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-10)
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
        var customer = new Customer
        {
            Email = "logout@test.com",
            FullName = "Logout Customer",
            Phone = "05006665544",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = "hashed-password"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = sessionId,
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
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
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, sessionId.ToString()),
                new Claim(AuthClaimTypes.PrincipalType, AuthPrincipalType.Customer.ToString())
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
    public async Task Logout_WhenSessionExistsButBelongsToDifferentPrincipal_ReturnsSuccessWithoutRevokingSession()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "mismatch@test.com",
            FullName = "Mismatch Customer",
            Phone = "05005554433",
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0,
            PasswordHash = "hashed-password"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var persistedSessionId = Guid.NewGuid();
        dbContext.AuthSessions.Add(new AuthSession
        {
            Id = persistedSessionId,
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = Guid.NewGuid(),
            RefreshTokenHash = "sha256:mismatch-refresh-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            LastSeenAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var refreshTokenCookieService = new Mock<IRefreshTokenCookieService>();
        var controller = CreateController(dbContext, refreshTokenCookieService: refreshTokenCookieService.Object);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, persistedSessionId.ToString()),
                new Claim(AuthClaimTypes.PrincipalType, AuthPrincipalType.Customer.ToString())
            ],
            authenticationType: "Bearer"));

        var result = await controller.Logout(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        payloadJson.Should().Contain("\"Success\":true");

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.Id.Should().Be(persistedSessionId);
        persistedSession.RevokedAtUtc.Should().BeNull();
        refreshTokenCookieService.Verify(service => service.ClearRefreshTokenCookie(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Me_WithCustomerPrincipal_ReturnsProfileData()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "me@test.com",
            FullName = "Me Customer",
            Phone = "05001234567",
            IdentityNumber = "12345678901",
            Nationality = "TR",
            LicenseYear = 2018,
            BirthDate = new DateOnly(1995, 3, 15)
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString())
            ],
            authenticationType: "Bearer"));

        var result = await controller.Me(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CustomerProfileResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Id.Should().Be(customer.Id);
        response.Data.Email.Should().Be(customer.Email);
        response.Data.FullName.Should().Be(customer.FullName);
        response.Data.Phone.Should().Be(customer.Phone);
        response.Data.IdentityNumber.Should().Be(customer.IdentityNumber);
        response.Data.Nationality.Should().Be(customer.Nationality);
        response.Data.LicenseYear.Should().Be(customer.LicenseYear);
        response.Data.BirthDate.Should().Be(customer.BirthDate);
    }

    [Fact]
    public async Task Me_WithInvalidPrincipalIdClaim_ReturnsUnauthorized()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
            ],
            authenticationType: "Bearer"));

        var result = await controller.Me(CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeOfType<ApiResponse<object>>().Which.Message.Should().Be("Yetkisiz erişim");
    }

    private static CustomerAuthController CreateController(
        IApplicationDbContext dbContext,
        IPasswordHasher? passwordHasher = null,
        IJwtTokenService? jwtTokenService = null,
        IRefreshTokenCookieService? refreshTokenCookieService = null,
        IAuditLogService? auditLogService = null,
        RefreshTokenCookieSettings? refreshTokenCookieSettings = null)
    {
        passwordHasher ??= new Mock<IPasswordHasher>().Object;
        jwtTokenService ??= Mock.Of<IJwtTokenService>();
        refreshTokenCookieService ??= Mock.Of<IRefreshTokenCookieService>();
        auditLogService ??= Mock.Of<IAuditLogService>();
        refreshTokenCookieSettings ??= new RefreshTokenCookieSettings();

        return new CustomerAuthController(
            dbContext,
            passwordHasher,
            jwtTokenService,
            refreshTokenCookieService,
            auditLogService,
            Options.Create(refreshTokenCookieSettings))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static void SetRefreshCookie(HttpContext httpContext, string cookieName, string refreshToken)
    {
        httpContext.Request.Headers.Cookie = $"{cookieName}={refreshToken}";
    }

    #region UpdateProfile Tests

    [Fact]
    public async Task UpdateProfile_WithValidData_UpdatesCustomerAndReturnsSuccess()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "profile@test.com",
            FullName = "Original Name",
            Phone = "05001110000",
            IdentityNumber = "12345678901",
            Nationality = "TR",
            LicenseYear = 2015,
            BirthDate = new DateOnly(1990, 5, 15)
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString())
            ],
            authenticationType: "Bearer"));

        var request = new UpdateProfileRequest(
            FullName: "Updated Name",
            Phone: "05009998877",
            IdentityNumber: "98765432109",
            Nationality: "US",
            LicenseYear: 2018,
            BirthDate: new DateOnly(1992, 8, 20));

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var persistedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        persistedCustomer.FullName.Should().Be("Updated Name");
        persistedCustomer.Phone.Should().Be("05009998877");
        persistedCustomer.IdentityNumber.Should().Be("98765432109");
        persistedCustomer.Nationality.Should().Be("US");
        persistedCustomer.LicenseYear.Should().Be(2018);
        persistedCustomer.BirthDate.Should().Be(new DateOnly(1992, 8, 20));
    }

    [Fact]
    public async Task UpdateProfile_WithPartialData_UpdatesOnlyProvidedFields()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "partial@test.com",
            FullName = "Original Name",
            Phone = "05001110000",
            IdentityNumber = "12345678901",
            Nationality = "TR",
            LicenseYear = 2015
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString())
            ],
            authenticationType: "Bearer"));

        var request = new UpdateProfileRequest(
            FullName: "New Name",
            Phone: null,
            IdentityNumber: null,
            Nationality: null,
            LicenseYear: null,
            BirthDate: null);

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var persistedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        persistedCustomer.FullName.Should().Be("New Name");
        persistedCustomer.Phone.Should().Be("05001110000");
        persistedCustomer.IdentityNumber.Should().Be("12345678901");
        persistedCustomer.Nationality.Should().Be("TR");
        persistedCustomer.LicenseYear.Should().Be(2015);
    }

    [Fact]
    public async Task UpdateProfile_WithWhitespaceAndInvalidBoundaryValues_PreservesExistingFields()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "no-op@test.com",
            FullName = "Original Name",
            Phone = "05001110000",
            IdentityNumber = "12345678901",
            Nationality = "TR",
            LicenseYear = 2015,
            BirthDate = new DateOnly(1991, 1, 2)
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString())
            ],
            authenticationType: "Bearer"));

        var request = new UpdateProfileRequest(
            FullName: "   ",
            Phone: " ",
            IdentityNumber: "   ",
            Nationality: null,
            LicenseYear: 1899,
            BirthDate: null);

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var persistedCustomer = dbContext.Customers.Should().ContainSingle().Subject;
        persistedCustomer.FullName.Should().Be("Original Name");
        persistedCustomer.Phone.Should().Be("05001110000");
        persistedCustomer.IdentityNumber.Should().Be("12345678901");
        persistedCustomer.Nationality.Should().Be("TR");
        persistedCustomer.LicenseYear.Should().Be(2015);
        persistedCustomer.BirthDate.Should().Be(new DateOnly(1991, 1, 2));
    }

    [Fact]
    public async Task UpdateProfile_WithLicenseYearAtBoundary1900_UpdatesLicenseYear()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var customer = new Customer
        {
            Email = "boundary@test.com",
            FullName = "Boundary Name",
            Phone = "05001110000",
            IdentityNumber = "12345678901",
            Nationality = "TR",
            LicenseYear = 2015
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString())
            ],
            authenticationType: "Bearer"));

        var request = new UpdateProfileRequest(
            FullName: null,
            Phone: null,
            IdentityNumber: null,
            Nationality: null,
            LicenseYear: 1900,
            BirthDate: null);

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        dbContext.Customers.Should().ContainSingle().Which.LicenseYear.Should().Be(1900);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var request = new UpdateProfileRequest(
            FullName: "Test",
            Phone: null,
            IdentityNumber: null,
            Nationality: null,
            LicenseYear: null,
            BirthDate: null);

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_WithNonExistentCustomer_ReturnsNotFound()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
            ],
            authenticationType: "Bearer"));

        var request = new UpdateProfileRequest(
            FullName: "Ghost User",
            Phone: null,
            IdentityNumber: null,
            Nationality: null,
            LicenseYear: null,
            BirthDate: null);

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }


    private static object? ReadProperty(object value, string propertyName) =>
        value.GetType().GetProperty(propertyName)?.GetValue(value);

    private static bool ReadBoolProperty(object value, string propertyName) =>
        (bool)(ReadProperty(value, propertyName) ?? throw new InvalidOperationException($"Missing property: {propertyName}"));

    private static string ReadStringProperty(object value, string propertyName) =>
        (string)(ReadProperty(value, propertyName) ?? throw new InvalidOperationException($"Missing property: {propertyName}"));
    #endregion
}



