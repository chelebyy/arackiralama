using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

public sealed class AdminUsersControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminUsersControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetAll_ReturnsOrderedAdminUsers()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.AdminUsers.AddRange(
            CreateAdminUser("zeta-admin@test.com", role: AuthRoleNames.Admin),
            CreateAdminUser("alpha-admin@test.com", role: AuthRoleNames.SuperAdmin));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<AdminUserDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Should().HaveCount(2);
        response.Data[0].Email.Should().Be("alpha-admin@test.com");
        response.Data[0].Role.Should().Be(AuthRoleNames.SuperAdmin);
        response.Data[1].Email.Should().Be("zeta-admin@test.com");
        response.Data[1].Role.Should().Be(AuthRoleNames.Admin);
    }

    [Fact]
    public async Task Create_WithValidPayload_PersistsAdminUserWithCanonicalRoleAndHashedPassword()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var passwordHasher = new BcryptPasswordHasher();
        var controller = CreateController(dbContext, passwordHasher: passwordHasher);

        var result = await controller.Create(
            new AdminUserCreateRequest(
                Email: " New.Admin@Test.com ",
                Password: "P@ssw0rd!",
                FullName: " New Admin ",
                Role: AuthRoleNames.Admin),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminUserDto>>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be("Yonetici kullanicisi olusturuldu.");
        response.Data.Should().NotBeNull();
        response.Data!.Role.Should().Be(AuthRoleNames.Admin);
        response.Data.Email.Should().Be("New.Admin@Test.com");

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.Email.Should().Be("New.Admin@Test.com");
        persistedAdmin.NormalizedEmail.Should().Be("NEW.ADMIN@TEST.COM");
        persistedAdmin.FullName.Should().Be("New Admin");
        persistedAdmin.Role.Should().Be(AuthRoleNames.Admin);
        persistedAdmin.IsActive.Should().BeTrue();
        passwordHasher.VerifyPassword("P@ssw0rd!", persistedAdmin.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithRoleCasingDrift_ReturnsBadRequestAndDoesNotPersist()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.Create(
            new AdminUserCreateRequest(
                Email: "admin@test.com",
                Password: "P@ssw0rd!",
                FullName: "Admin Test",
                Role: "admin"),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Rol yalnizca Admin veya SuperAdmin olabilir.");
        dbContext.AdminUsers.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateRole_WithSupportedRole_UpdatesAdminRole()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("update-role@test.com", role: AuthRoleNames.Admin);
        dbContext.AdminUsers.Add(admin);
        var session = CreateActiveAdminSession(admin.Id);
        dbContext.AuthSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.UpdateRole(
            admin.Id,
            new AdminUserUpdateRoleRequest(AuthRoleNames.SuperAdmin),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminUserDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Role.Should().Be(AuthRoleNames.SuperAdmin);

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.Role.Should().Be(AuthRoleNames.SuperAdmin);
        persistedAdmin.TokenVersion.Should().Be(1);

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.RevokedAtUtc.Should().NotBeNull();
        persistedSession.LastSeenAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_WithValidPayload_UpdatesProfileAndRevokesSessions()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("profile@test.com", role: AuthRoleNames.Admin);
        dbContext.AdminUsers.Add(admin);
        dbContext.AuthSessions.Add(CreateActiveAdminSession(admin.Id));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.Update(
            admin.Id,
            new AdminUserUpdateRequest(
                Email: " Updated.Profile@Test.com ",
                FullName: " Updated Profile ",
                Role: AuthRoleNames.SuperAdmin),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminUserDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Yonetici kullanicisi guncellendi.");
        response.Data.Should().NotBeNull();
        response.Data!.Email.Should().Be("Updated.Profile@Test.com");
        response.Data.FullName.Should().Be("Updated Profile");
        response.Data.Role.Should().Be(AuthRoleNames.SuperAdmin);

        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.NormalizedEmail.Should().Be("UPDATED.PROFILE@TEST.COM");
        persistedAdmin.TokenVersion.Should().Be(1);

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.RevokedAtUtc.Should().NotBeNull();
        persistedSession.LastSeenAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_WithDuplicateEmail_ReturnsBadRequestAndKeepsOriginalValues()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("profile@test.com", role: AuthRoleNames.Admin);
        var otherAdmin = CreateAdminUser("taken@test.com", role: AuthRoleNames.Admin);
        dbContext.AdminUsers.AddRange(admin, otherAdmin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.Update(
            admin.Id,
            new AdminUserUpdateRequest(
                Email: "TAKEN@test.com",
                FullName: "Updated Profile",
                Role: AuthRoleNames.SuperAdmin),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Bu email ile kayitli bir yonetici zaten var.");

        var persistedAdmin = dbContext.AdminUsers.Single(user => user.Id == admin.Id);
        persistedAdmin.Email.Should().Be("profile@test.com");
        persistedAdmin.FullName.Should().Be("Admin User");
        persistedAdmin.Role.Should().Be(AuthRoleNames.Admin);
    }

    [Fact]
    public async Task Update_WhenLastActiveSuperAdminWouldBeDemoted_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var superAdmin = CreateAdminUser("last-super@test.com", role: AuthRoleNames.SuperAdmin);
        dbContext.AdminUsers.Add(superAdmin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.Update(
            superAdmin.Id,
            new AdminUserUpdateRequest(
                Email: superAdmin.Email,
                FullName: superAdmin.FullName,
                Role: AuthRoleNames.Admin),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Son aktif SuperAdmin kullanicisinin rolu dusurulemez.");
        dbContext.AdminUsers.Should().ContainSingle().Which.Role.Should().Be(AuthRoleNames.SuperAdmin);
    }

    [Fact]
    public async Task UpdateRole_WithInvalidRole_ReturnsBadRequestAndKeepsRole()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("keep-role@test.com", role: AuthRoleNames.Admin);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.UpdateRole(
            admin.Id,
            new AdminUserUpdateRoleRequest("SUPERADMIN"),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Rol yalnizca Admin veya SuperAdmin olabilir.");
        dbContext.AdminUsers.Should().ContainSingle().Which.Role.Should().Be(AuthRoleNames.Admin);
    }

    [Fact]
    public async Task Deactivate_WithExistingAdmin_SetsIsActiveFalse()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("deactivate@test.com", isActive: true);
        dbContext.AdminUsers.Add(admin);
        var session = CreateActiveAdminSession(admin.Id);
        dbContext.AuthSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Deactivate(admin.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminUserDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IsActive.Should().BeFalse();
        var persistedAdmin = dbContext.AdminUsers.Should().ContainSingle().Subject;
        persistedAdmin.IsActive.Should().BeFalse();
        persistedAdmin.TokenVersion.Should().Be(1);

        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.RevokedAtUtc.Should().NotBeNull();
        persistedSession.LastSeenAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Deactivate_WhenCurrentUser_ReturnsBadRequestAndKeepsActive()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("self-deactivate@test.com", role: AuthRoleNames.SuperAdmin, isActive: true);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetCurrentAdmin(controller, admin.Id);

        var result = await controller.Deactivate(admin.Id, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Kendi yonetici hesabinizi pasife alamazsiniz.");
        dbContext.AdminUsers.Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_WhenLastActiveSuperAdmin_ReturnsBadRequestAndKeepsActive()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var superAdmin = CreateAdminUser("last-active-super@test.com", role: AuthRoleNames.SuperAdmin, isActive: true);
        dbContext.AdminUsers.Add(superAdmin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.Deactivate(superAdmin.Id, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Son aktif SuperAdmin kullanicisi pasife alinamaz.");
        dbContext.AdminUsers.Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Activate_WithExistingAdmin_SetsIsActiveTrue()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("activate@test.com", isActive: false);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Activate(admin.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminUserDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IsActive.Should().BeTrue();
        dbContext.AdminUsers.Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithExistingAdmin_RemovesAdminAndRevokesSessions()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var superAdmin = CreateAdminUser("owner@test.com", role: AuthRoleNames.SuperAdmin);
        var admin = CreateAdminUser("delete@test.com", role: AuthRoleNames.Admin);
        dbContext.AdminUsers.AddRange(superAdmin, admin);
        dbContext.AuthSessions.Add(CreateActiveAdminSession(admin.Id));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetCurrentAdmin(controller, superAdmin.Id);

        var result = await controller.Delete(admin.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        payloadJson.Should().Contain("\"Success\":true");
        payloadJson.Should().Contain("\"Message\":\"Yonetici kullanicisi silindi.\"");

        dbContext.AdminUsers.Should().ContainSingle().Which.Email.Should().Be("owner@test.com");
        var persistedSession = dbContext.AuthSessions.Should().ContainSingle().Subject;
        persistedSession.RevokedAtUtc.Should().NotBeNull();
        persistedSession.LastSeenAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_WhenCurrentUser_ReturnsBadRequestAndKeepsAdmin()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("self-delete@test.com", role: AuthRoleNames.SuperAdmin);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetCurrentAdmin(controller, admin.Id);

        var result = await controller.Delete(admin.Id, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Kendi yonetici hesabinizi silemezsiniz.");
        dbContext.AdminUsers.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_WhenLastActiveSuperAdmin_ReturnsBadRequestAndKeepsAdmin()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var superAdmin = CreateAdminUser("last-delete-super@test.com", role: AuthRoleNames.SuperAdmin, isActive: true);
        dbContext.AdminUsers.Add(superAdmin);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.Delete(superAdmin.Id, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Son aktif SuperAdmin kullanicisi silinemez.");
        dbContext.AdminUsers.Should().ContainSingle();
    }

    [Fact]
    public async Task InitiatePasswordReset_WithActiveAdmin_PersistsResetTokenAndInvokesDispatcher()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("reset-active@test.com", isActive: true);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var jwtTokenService = new Mock<IJwtTokenService>();
        jwtTokenService
            .Setup(service => service.HashRefreshToken(It.IsAny<string>()))
            .Returns((string token) => $"sha256:{token}");

        string? expectedTokenHash = null;
        DateTime expectedExpiresAtUtc = default;

        var dispatcher = new Mock<IPasswordResetEmailDispatcher>();
        dispatcher
            .Setup(service => service.DispatchAsync(
                AuthPrincipalType.Admin,
                admin.Email,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<AuthPrincipalType, string, string, DateTime, string, CancellationToken>((_, _, token, expiresAtUtc, _, _) =>
            {
                expectedTokenHash = $"sha256:{token}";
                expectedExpiresAtUtc = expiresAtUtc;
            })
            .Returns(Task.CompletedTask);

        var controller = CreateController(
            dbContext,
            jwtTokenService: jwtTokenService.Object,
            emailDispatcher: dispatcher.Object);

        var result = await controller.InitiatePasswordReset(admin.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        payloadJson.Should().Contain("\"Success\":true");
        payloadJson.Should().Contain("\"Message\":\"Yonetici sifre sifirlama baglantisi gonderimi baslatildi.\"");

        var resetToken = dbContext.PasswordResetTokens.Should().ContainSingle().Subject;
        resetToken.PrincipalType.Should().Be(AuthPrincipalType.Admin);
        resetToken.PrincipalId.Should().Be(admin.Id);
        resetToken.TokenHash.Should().StartWith("sha256:");
        resetToken.TokenHash.Should().Be(expectedTokenHash);
        resetToken.ExpiresAtUtc.Should().Be(expectedExpiresAtUtc);
        resetToken.ConsumedAtUtc.Should().BeNull();

        dispatcher.Verify(
            service => service.DispatchAsync(
                AuthPrincipalType.Admin,
                admin.Email,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitiatePasswordReset_WithInactiveAdmin_ReturnsBadRequestAndSkipsDispatch()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var admin = CreateAdminUser("reset-inactive@test.com", isActive: false);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();

        var dispatcher = new Mock<IPasswordResetEmailDispatcher>();
        var controller = CreateController(dbContext, emailDispatcher: dispatcher.Object);

        var result = await controller.InitiatePasswordReset(admin.Id, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Sadece aktif yonetici kullanicilari icin sifre sifirlama baslatilabilir.");
        dbContext.PasswordResetTokens.Should().BeEmpty();

        dispatcher.Verify(
            service => service.DispatchAsync(
                It.IsAny<AuthPrincipalType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AdminUsersController CreateController(
        IApplicationDbContext dbContext,
        IPasswordHasher? passwordHasher = null,
        IJwtTokenService? jwtTokenService = null,
        IPasswordResetEmailDispatcher? emailDispatcher = null)
    {
        passwordHasher ??= new BcryptPasswordHasher();
        jwtTokenService ??= Mock.Of<IJwtTokenService>();
        emailDispatcher ??= Mock.Of<IPasswordResetEmailDispatcher>();

        return new AdminUsersController(
            dbContext,
            passwordHasher,
            jwtTokenService,
            emailDispatcher,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<AdminUsersController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AdminUser CreateAdminUser(string email, string role = AuthRoleNames.Admin, bool isActive = true)
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = new BcryptPasswordHasher().HashPassword("P@ssw0rd!"),
            FullName = "Admin User",
            Role = role,
            IsActive = isActive
        };
    }

    private static AuthSession CreateActiveAdminSession(Guid adminId)
    {
        return new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminId,
            RefreshTokenHash = "session-hash",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
    }

    private static void SetCurrentAdmin(AdminUsersController controller, Guid adminId)
    {
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                    new Claim(ClaimTypes.Role, AuthRoleNames.SuperAdmin)
                ],
                "TestAuth"));
    }
}
