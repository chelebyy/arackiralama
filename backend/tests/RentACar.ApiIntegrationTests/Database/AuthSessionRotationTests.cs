using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Auth;
using RentACar.API.Controllers;
using RentACar.API.Options;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Security;
using RentACar.Infrastructure.Services;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.ApiIntegrationTests.Database;

public sealed class AuthSessionRotationTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private const string OldPassword = "OriginalPassword1!";
    private const string NewPassword = "ReplacementPassword1!";
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(new JwtOptions
    {
        Secret = TestJwtFactory.JwtSecret,
        Issuer = TestJwtFactory.JwtIssuer,
        Audience = TestJwtFactory.JwtAudience
    }));

    [Theory]
    [InlineData(AuthPrincipalType.Admin)]
    [InlineData(AuthPrincipalType.Customer)]
    public async Task ConcurrentRefresh_ConsumesParentExactlyOnce(AuthPrincipalType kind)
    {
        var seed = await SeedAsync(kind);
        await using var firstDb = CreateDb();
        await using var secondDb = CreateDb();
        var arrivals = 0;
        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Rendezvous()
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                bothReady.TrySetResult();
            }
            await bothReady.Task.WaitAsync(Timeout);
        }

        var results = await Task.WhenAll(
            RefreshAsync(kind, firstDb, seed.RefreshToken,
                new CoordinatedRotation(new AuthSessionRotationService(firstDb), Rendezvous)),
            RefreshAsync(kind, secondDb, seed.RefreshToken,
                new CoordinatedRotation(new AuthSessionRotationService(secondDb), Rendezvous))).WaitAsync(Timeout);

        results.Count(result => result.Result is OkObjectResult).Should().Be(1);
        var loser = results.Single(result => result.Result is UnauthorizedObjectResult);
        loser.Http.Response.Headers.SetCookie.Should().BeEmpty();
        await using var verifyDb = CreateDb();
        var sessions = await verifyDb.AuthSessions.Where(session => session.PrincipalId == seed.PrincipalId).ToListAsync();
        sessions.Should().HaveCount(2);
        var parent = sessions.Single(session => session.Id == seed.SessionId);
        parent.RevokedAtUtc.Should().NotBeNull();
        var child = sessions.Single(session => session.Id != seed.SessionId);
        parent.ReplacedBySessionId.Should().Be(child.Id);
        child.TokenVersion.Should().Be(3);
        child.RevokedAtUtc.Should().BeNull();
        var winner = results.Single(result => result.Result is OkObjectResult);
        (await ValidateAsync(winner.AccessToken)).Should().Be(AccessTokenSessionValidationFailure.None);

        await using var replayDb = CreateDb();
        (await RefreshAsync(kind, replayDb, seed.RefreshToken)).Result.Should().BeOfType<UnauthorizedObjectResult>();
        await using var nextDb = CreateDb();
        (await RefreshAsync(kind, nextDb, winner.RefreshToken)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData(AuthPrincipalType.Admin)]
    [InlineData(AuthPrincipalType.Customer)]
    public async Task ResetBeforeRefreshConsumption_RejectsPreviouslyReadSession(AuthPrincipalType kind)
    {
        var seed = await SeedAsync(kind);
        var pause = new Pause();
        await using var refreshDb = CreateDb();
        var refresh = RefreshAsync(kind, refreshDb, seed.RefreshToken,
            new CoordinatedRotation(new AuthSessionRotationService(refreshDb), pause.WaitAsync));
        try
        {
            await pause.Reached.Task.WaitAsync(Timeout);
            await using var resetDb = CreateDb();
            (await ResetAsync(kind, resetDb, seed.ResetToken)).Should().BeOfType<OkObjectResult>();
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var result = await refresh.WaitAsync(Timeout);
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        result.Http.Response.Headers.SetCookie.Should().BeEmpty();
        await AssertFreshLoginAndRefreshAsync(kind, seed);
    }

    [Theory]
    [InlineData(AuthPrincipalType.Admin)]
    [InlineData(AuthPrincipalType.Customer)]
    public async Task RefreshAfterResetSessionQuery_CannotAuthenticateAfterReset(AuthPrincipalType kind)
    {
        var seed = await SeedAsync(kind);
        var pause = new Pause();
        await using var resetDb = CreateDb(new PauseBeforeSave(pause));
        var reset = ResetAsync(kind, resetDb, seed.ResetToken);
        AuthResult rotated;
        try
        {
            await pause.Reached.Task.WaitAsync(Timeout);
            await using var refreshDb = CreateDb();
            rotated = await RefreshAsync(kind, refreshDb, seed.RefreshToken);
            rotated.Result.Should().BeOfType<OkObjectResult>();
        }
        finally
        {
            pause.Resume.TrySetResult();
        }
        (await reset.WaitAsync(Timeout)).Should().BeOfType<OkObjectResult>();

        await using var verifyDb = CreateDb();
        var child = await verifyDb.AuthSessions.SingleAsync(session => session.PrincipalId == seed.PrincipalId
            && session.Id != seed.SessionId);
        child.RevokedAtUtc.Should().BeNull();
        child.TokenVersion.Should().Be(3);
        (await ValidateAsync(rotated.AccessToken)).Should().Be(AccessTokenSessionValidationFailure.TokenVersionMismatch);
        await using var replayDb = CreateDb();
        var replay = await RefreshAsync(kind, replayDb, rotated.RefreshToken);
        replay.Result.Should().BeOfType<UnauthorizedObjectResult>();
        replay.Http.Response.Headers.SetCookie.Should().BeEmpty();
        await AssertFreshLoginAndRefreshAsync(kind, seed);
    }

    [Theory]
    [InlineData(AuthPrincipalType.Admin)]
    [InlineData(AuthPrincipalType.Customer)]
    public async Task FailedChildInsert_RollsBackParentConsumption(AuthPrincipalType kind)
    {
        var seed = await SeedAsync(kind);
        await using (var db = CreateDb())
        {
            var replacement = new AuthSession
            {
                PrincipalId = seed.PrincipalId,
                PrincipalType = kind,
                TokenVersion = 3,
                RefreshTokenHash = _jwt.HashRefreshToken(seed.RefreshToken),
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };
            var act = () => new AuthSessionRotationService(db).TryRotateAsync(seed.SessionId, replacement, CancellationToken.None);
            await act.Should().ThrowAsync<DbUpdateException>();
        }
        await using var verifyDb = CreateDb();
        var parent = await verifyDb.AuthSessions.SingleAsync(session => session.PrincipalId == seed.PrincipalId);
        parent.RevokedAtUtc.Should().BeNull();
        parent.ReplacedBySessionId.Should().BeNull();
        await using var retryDb = CreateDb();
        (await RefreshAsync(kind, retryDb, seed.RefreshToken)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData(AuthPrincipalType.Admin, null)]
    [InlineData(AuthPrincipalType.Customer, null)]
    [InlineData(AuthPrincipalType.Admin, 2)]
    [InlineData(AuthPrincipalType.Customer, 2)]
    public async Task UnboundOrStaleSession_CannotRefresh(AuthPrincipalType kind, int? version)
    {
        var seed = await SeedAsync(kind);
        await using var db = CreateDb();
        await db.AuthSessions.Where(session => session.Id == seed.SessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.TokenVersion, version));

        var result = await RefreshAsync(kind, db, seed.RefreshToken);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        result.Http.Response.Headers.SetCookie.Should().BeEmpty();
        (await db.AuthSessions.CountAsync(session => session.PrincipalId == seed.PrincipalId)).Should().Be(1);
    }

    private RentACarDbContext CreateDb(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<RentACarDbContext>().UseNpgsql(database.ConnectionString)
            .AddInterceptors(interceptors).Options);

    private async Task<Seed> SeedAsync(AuthPrincipalType kind)
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();
        var email = $"rotation-{id:N}@test.invalid";
        var passwordHash = _hasher.HashPassword(OldPassword);
        if (kind == AuthPrincipalType.Admin)
        {
            db.AdminUsers.Add(new AdminUser
            {
                Id = id,
                Email = email,
                PasswordHash = passwordHash,
                Role = AuthRoleNames.SuperAdmin,
                IsActive = true,
                TokenVersion = 3
            });
        }
        else
        {
            db.Customers.Add(new Customer { Id = id, Email = email, PasswordHash = passwordHash, TokenVersion = 3 });
        }
        var refreshToken = _jwt.CreateRefreshToken(out var expires);
        var resetToken = _jwt.CreateRefreshToken(out _);
        var session = new AuthSession
        {
            PrincipalType = kind,
            PrincipalId = id,
            TokenVersion = 3,
            RefreshTokenHash = _jwt.HashRefreshToken(refreshToken),
            RefreshTokenExpiresAtUtc = expires
        };
        db.AuthSessions.Add(session);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            PrincipalType = kind,
            PrincipalId = id,
            TokenHash = _jwt.HashRefreshToken(resetToken),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();
        return new Seed(id, session.Id, email, refreshToken, resetToken);
    }

    private async Task<AuthResult> RefreshAsync(AuthPrincipalType kind, RentACarDbContext db, string token,
        IAuthSessionRotationService? rotation = null)
    {
        var controller = CreateController(kind, db, rotation);
        controller.HttpContext.Request.Headers.Cookie = $"__Host-rac_refresh={token}";
        var result = controller is AdminAuthController admin
            ? await admin.Refresh(CancellationToken.None)
            : await ((CustomerAuthController)controller).Refresh(CancellationToken.None);
        return new AuthResult(result, controller.HttpContext);
    }

    private ControllerBase CreateController(AuthPrincipalType kind, RentACarDbContext db, IAuthSessionRotationService? rotation = null)
    {
        rotation ??= new AuthSessionRotationService(db);
        var settings = Options.Create(new RefreshTokenCookieSettings());
        var cookies = new RefreshTokenCookieService(settings);
        ControllerBase controller = kind == AuthPrincipalType.Admin
            ? new AdminAuthController(db, _hasher, _jwt, rotation, cookies, settings)
            : new CustomerAuthController(db, _hasher, _jwt, rotation, cookies, null!, settings, null!,
                Options.Create(new NotificationOptions()), Options.Create(new AccountClaimSecurityOptions()),
                TimeProvider.System, NullLogger<CustomerAuthController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private Task<IActionResult> ResetAsync(AuthPrincipalType kind, RentACarDbContext db, string token) =>
        new PasswordResetController(db, _hasher, _jwt, null!, NullLogger<PasswordResetController>.Instance)
            .Confirm(new PasswordResetConfirmRequest(token, NewPassword, kind.ToString()), CancellationToken.None);

    private async Task<AccessTokenSessionValidationFailure> ValidateAsync(string accessToken)
    {
        await using var db = CreateDb();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return await new AccessTokenSessionValidator(db, NullLogger<AccessTokenSessionValidator>.Instance)
            .ValidateAsync(new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "Bearer")));
    }

    private async Task AssertFreshLoginAndRefreshAsync(AuthPrincipalType kind, Seed seed)
    {
        await using var loginDb = CreateDb();
        var controller = CreateController(kind, loginDb);
        var login = controller is AdminAuthController admin
            ? await admin.Login(new AdminLoginRequest(seed.Email, NewPassword), CancellationToken.None)
            : await ((CustomerAuthController)controller).Login(new CustomerLoginRequest(seed.Email, NewPassword), CancellationToken.None);
        login.Should().BeOfType<OkObjectResult>();
        var loginResult = new AuthResult(login, controller.HttpContext);
        (await ValidateAsync(loginResult.AccessToken)).Should().Be(AccessTokenSessionValidationFailure.None);
        await using var refreshDb = CreateDb();
        var refreshed = await RefreshAsync(kind, refreshDb, loginResult.RefreshToken);
        refreshed.Result.Should().BeOfType<OkObjectResult>();
        (await ValidateAsync(refreshed.AccessToken)).Should().Be(AccessTokenSessionValidationFailure.None);
    }

    private sealed record Seed(Guid PrincipalId, Guid SessionId, string Email, string RefreshToken, string ResetToken);

    private sealed record AuthResult(IActionResult Result, HttpContext Http)
    {
        public string RefreshToken => Http.Response.GetTypedHeaders().SetCookie!.Single().Value.Value!;
        public string AccessToken => ((OkObjectResult)Result).Value switch
        {
            ApiResponse<AdminRefreshResponse> value => value.Data!.AccessToken,
            ApiResponse<CustomerRefreshResponse> value => value.Data!.AccessToken,
            ApiResponse<AdminLoginResponse> value => value.Data!.AccessToken,
            ApiResponse<CustomerAuthResponse> value => value.Data!.AccessToken,
            _ => throw new InvalidOperationException("Unexpected auth response.")
        };
    }

    private sealed class CoordinatedRotation(IAuthSessionRotationService inner, Func<Task> before) : IAuthSessionRotationService
    {
        public async Task<bool> TryRotateAsync(Guid currentSessionId, AuthSession replacement, CancellationToken cancellationToken)
        {
            await before();
            return await inner.TryRotateAsync(currentSessionId, replacement, cancellationToken);
        }
    }

    private sealed class Pause
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync()
        {
            Reached.TrySetResult();
            await Resume.Task.WaitAsync(Timeout);
        }
    }

    private sealed class PauseBeforeSave(Pause pause) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await pause.WaitAsync();
            return result;
        }
    }
}
