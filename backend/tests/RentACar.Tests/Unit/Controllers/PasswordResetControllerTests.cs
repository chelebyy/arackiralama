using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
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

public class PasswordResetControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public PasswordResetControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task Request_WithUnknownEmail_ReturnsNonEnumeratingSuccessAndSkipsDispatch()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var jwtTokenService = new Mock<IJwtTokenService>();
        var dispatcher = new Mock<IPasswordResetEmailDispatcher>();
        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            emailDispatcher: dispatcher.Object);

        var result = await controller.RequestReset(new PasswordResetRequest("missing-admin@test.com", "Admin"), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        AssertOkApiResponseWithMessage(okResult, "Parola sıfırlama isteği alındı.");

        dbContext.PasswordResetTokens.Should().BeEmpty();
        dispatcher.Verify(
            service => service.DispatchAsync(
                It.IsAny<AuthPrincipalType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Request_WithInactiveAdmin_ReturnsNonEnumeratingSuccessAndSkipsDispatch()
    {
        using var dbContext = _dbContextFactory.CreateContext();

        dbContext.AdminUsers.Add(CreateAdminUser(isActive: false, email: "inactive-admin@test.com"));
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        var dispatcher = new Mock<IPasswordResetEmailDispatcher>();
        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            emailDispatcher: dispatcher.Object);

        var result = await controller.RequestReset(new PasswordResetRequest("inactive-admin@test.com", "Admin"), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        AssertOkApiResponseWithMessage(okResult, "Parola sıfırlama isteği alındı.");

        dbContext.PasswordResetTokens.Should().BeEmpty();
        dispatcher.Verify(
            service => service.DispatchAsync(
                It.IsAny<AuthPrincipalType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Request_WithActiveAdmin_PersistsHashedTokenAndInvokesDispatcher()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser(isActive: true, email: "Active.Admin@test.com");
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken(It.IsAny<string>()))
            .Returns((string token) => $"sha256:hash:{token}");

        string? dispatchedTokenHash = null;
        DateTime dispatchedExpiresAtUtc = default;

        var dispatcher = new Mock<IPasswordResetEmailDispatcher>();
        dispatcher
            .Setup(service => service.DispatchAsync(
                AuthPrincipalType.Admin,
                admin.Email,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<AuthPrincipalType, string, string, DateTime, CancellationToken>((_, _, token, expiresAtUtc, _) =>
            {
                dispatchedTokenHash = $"sha256:hash:{token}";
                dispatchedExpiresAtUtc = expiresAtUtc;
            })
            .Returns(Task.CompletedTask);

        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            emailDispatcher: dispatcher.Object);

        var result = await controller.RequestReset(new PasswordResetRequest(" active.admin@Test.com ", "Admin"), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        AssertOkApiResponseWithMessage(okResult, "Parola sıfırlama isteği alındı.");

        var persistedToken = dbContext.PasswordResetTokens.Should().ContainSingle().Subject;
        persistedToken.PrincipalType.Should().Be(AuthPrincipalType.Admin);
        persistedToken.PrincipalId.Should().Be(admin.Id);
        persistedToken.TokenHash.Should().StartWith("sha256:hash:");
        persistedToken.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(25));
        persistedToken.ExpiresAtUtc.Should().BeBefore(DateTime.UtcNow.AddMinutes(31));
        persistedToken.ConsumedAtUtc.Should().BeNull();

        dispatchedTokenHash.Should().NotBeNullOrWhiteSpace();
        persistedToken.TokenHash.Should().Be(dispatchedTokenHash);
        persistedToken.ExpiresAtUtc.Should().Be(dispatchedExpiresAtUtc);

        dispatcher.Verify(
            service => service.DispatchAsync(
                AuthPrincipalType.Admin,
                admin.Email,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Confirm_WithValidAdminToken_ConsumesTokenBumpsTokenVersionAndRevokesActiveSessions()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var admin = CreateAdminUser(isActive: true, email: "confirm-admin@test.com");
        admin.TokenVersion = 4;
        admin.PasswordHash = passwordHasher.HashPassword("OldP@ssw0rd!");

        dbContext.AdminUsers.Add(admin);

        var activeSessionId = Guid.NewGuid();
        var secondActiveSessionId = Guid.NewGuid();
        var expiredSessionId = Guid.NewGuid();
        var differentPrincipalSessionId = Guid.NewGuid();

        dbContext.AuthSessions.AddRange(
            new AuthSession
            {
                Id = activeSessionId,
                PrincipalType = AuthPrincipalType.Admin,
                PrincipalId = admin.Id,
                RefreshTokenHash = "sha256:active-1",
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(4)
            },
            new AuthSession
            {
                Id = secondActiveSessionId,
                PrincipalType = AuthPrincipalType.Admin,
                PrincipalId = admin.Id,
                RefreshTokenHash = "sha256:active-2",
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(2)
            },
            new AuthSession
            {
                Id = expiredSessionId,
                PrincipalType = AuthPrincipalType.Admin,
                PrincipalId = admin.Id,
                RefreshTokenHash = "sha256:expired",
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            },
            new AuthSession
            {
                Id = differentPrincipalSessionId,
                PrincipalType = AuthPrincipalType.Admin,
                PrincipalId = Guid.NewGuid(),
                RefreshTokenHash = "sha256:other",
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(4)
            });

        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = admin.Id,
            TokenHash = "sha256:valid-reset-token-hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20)
        });

        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("valid-reset-token"))
            .Returns("sha256:valid-reset-token-hash");

        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object,
            emailDispatcher: Mock.Of<IPasswordResetEmailDispatcher>());

        var result = await controller.Confirm(
            new PasswordResetConfirmRequest("valid-reset-token", "N3wP@ssw0rd!", "Admin"),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        AssertOkApiResponseWithMessage(okResult, "Parola başarıyla güncellendi.");

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.TokenVersion.Should().Be(5);
        passwordHasher.VerifyPassword("N3wP@ssw0rd!", persistedAdmin.PasswordHash).Should().BeTrue();
        passwordHasher.VerifyPassword("OldP@ssw0rd!", persistedAdmin.PasswordHash).Should().BeFalse();

        var persistedResetToken = dbContext.PasswordResetTokens.Should().ContainSingle().Subject;
        persistedResetToken.TokenHash.Should().Be("sha256:valid-reset-token-hash");
        persistedResetToken.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(10));
        persistedResetToken.ConsumedAtUtc.Should().NotBeNull();

        dbContext.AuthSessions.Single(session => session.Id == activeSessionId).RevokedAtUtc.Should().NotBeNull();
        dbContext.AuthSessions.Single(session => session.Id == secondActiveSessionId).RevokedAtUtc.Should().NotBeNull();
        dbContext.AuthSessions.Single(session => session.Id == expiredSessionId).RevokedAtUtc.Should().BeNull();
        dbContext.AuthSessions.Single(session => session.Id == differentPrincipalSessionId).RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_WithInvalidToken_ReturnsBadRequestAndDoesNotMutateState()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var admin = CreateAdminUser(isActive: true, email: "invalid-token-admin@test.com");
        admin.TokenVersion = 2;
        admin.PasswordHash = passwordHasher.HashPassword("OldP@ssw0rd!");

        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("invalid-token"))
            .Returns("sha256:invalid-token-hash");

        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object);

        var result = await controller.Confirm(
            new PasswordResetConfirmRequest("invalid-token", "N3wP@ssw0rd!", "Admin"),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Geçersiz veya süresi dolmuş parola sıfırlama bağlantısı.");

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.TokenVersion.Should().Be(2);
        passwordHasher.VerifyPassword("OldP@ssw0rd!", persistedAdmin.PasswordHash).Should().BeTrue();
        dbContext.PasswordResetTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_WithExpiredToken_ReturnsBadRequestAndKeepsTokenUnconsumed()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var admin = CreateAdminUser(isActive: true, email: "expired-token-admin@test.com");
        admin.TokenVersion = 6;
        admin.PasswordHash = passwordHasher.HashPassword("OldP@ssw0rd!");

        dbContext.AdminUsers.Add(admin);
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = admin.Id,
            TokenHash = "sha256:expired-reset-token-hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-3),
            ConsumedAtUtc = null
        });
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("expired-reset-token"))
            .Returns("sha256:expired-reset-token-hash");

        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object);

        var result = await controller.Confirm(
            new PasswordResetConfirmRequest("expired-reset-token", "N3wP@ssw0rd!", "Admin"),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Geçersiz veya süresi dolmuş parola sıfırlama bağlantısı.");

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.TokenVersion.Should().Be(6);
        passwordHasher.VerifyPassword("OldP@ssw0rd!", persistedAdmin.PasswordHash).Should().BeTrue();

        var persistedToken = dbContext.PasswordResetTokens.Should().ContainSingle().Subject;
        persistedToken.TokenHash.Should().Be("sha256:expired-reset-token-hash");
        persistedToken.ExpiresAtUtc.Should().BeBefore(DateTime.UtcNow);
        persistedToken.ConsumedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_WithConsumedToken_ReturnsBadRequestAndKeepsConsumedState()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var admin = CreateAdminUser(isActive: true, email: "consumed-token-admin@test.com");
        admin.TokenVersion = 8;
        admin.PasswordHash = passwordHasher.HashPassword("OldP@ssw0rd!");

        dbContext.AdminUsers.Add(admin);
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = admin.Id,
            TokenHash = "sha256:consumed-reset-token-hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20),
            ConsumedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken("consumed-reset-token"))
            .Returns("sha256:consumed-reset-token-hash");

        var controller = CreateController(
            dbContext,
            passwordHasher: passwordHasher,
            jwtTokenService: jwtTokenService.Object);

        var result = await controller.Confirm(
            new PasswordResetConfirmRequest("consumed-reset-token", "N3wP@ssw0rd!", "Admin"),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Geçersiz veya süresi dolmuş parola sıfırlama bağlantısı.");

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.TokenVersion.Should().Be(8);
        passwordHasher.VerifyPassword("OldP@ssw0rd!", persistedAdmin.PasswordHash).Should().BeTrue();

        var persistedToken = dbContext.PasswordResetTokens.Should().ContainSingle().Subject;
        persistedToken.TokenHash.Should().Be("sha256:consumed-reset-token-hash");
        persistedToken.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        persistedToken.ConsumedAtUtc.Should().NotBeNull();
    }

    private static void AssertOkApiResponseWithMessage(OkObjectResult okResult, string expectedMessage)
    {
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        using var document = JsonDocument.Parse(payloadJson);

        document.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("Message").GetString().Should().Be(expectedMessage);
    }

    private static PasswordResetController CreateController(
        IApplicationDbContext dbContext,
        IPasswordHasher? passwordHasher = null,
        IJwtTokenService? jwtTokenService = null,
        IPasswordResetEmailDispatcher? emailDispatcher = null)
    {
        passwordHasher ??= new BcryptPasswordHasher();
        jwtTokenService ??= Mock.Of<IJwtTokenService>();
        emailDispatcher ??= Mock.Of<IPasswordResetEmailDispatcher>();

        return new PasswordResetController(
            dbContext,
            passwordHasher,
            jwtTokenService,
            emailDispatcher,
            Mock.Of<ILogger<PasswordResetController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AdminUser CreateAdminUser(bool isActive, string email)
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = new BcryptPasswordHasher().HashPassword("P@ssw0rd!"),
            FullName = "Reset Admin",
            Role = "Admin",
            IsActive = isActive
        };
    }
}
