using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Authentication;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Creates JWT access tokens that satisfy the API's production validation pipeline.
/// </summary>
public static class TestJwtFactory
{
    public const string JwtIssuer = "RentACar.API.Tests";
    public const string JwtAudience = "RentACar.Client.Tests";
    public const string JwtSecret = "IntegrationTestingSecretKeyAtLeast32Chars!";

    /// <summary>
    /// Creates a valid admin JWT token and backing auth session.
    /// </summary>
    public static async Task<string> CreateAdminTokenAsync(IServiceProvider services, string role = AuthRoleNames.Admin, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(admin => admin.NormalizedEmail == AdminUser.NormalizeEmail(TestDataSeeder.GetSeedAdminEmail()), cancellationToken);

        if (adminUser is null)
        {
            throw new InvalidOperationException("Seed admin user was not found in the integration test database.");
        }

        adminUser.Role = role;
        await dbContext.SaveChangesAsync(cancellationToken);

        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Admin,
            PrincipalId = adminUser.Id,
            RefreshTokenHash = $"test-refresh-{Guid.NewGuid():N}",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "integration-tests"
        };

        dbContext.AuthSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return jwtTokenService.CreateAdminAccessToken(adminUser, session.Id, out _);
    }

    /// <summary>
    /// Creates a valid customer JWT token and backing auth session.
    /// </summary>
    public static async Task<string> CreateCustomerTokenAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var email = $"customer-{Guid.NewGuid():N}@rentacar.test";
        var customer = new Customer
        {
            FullName = "Integration Test Customer",
            Phone = "+90 555 000 00 00",
            Email = email,
            LicenseYear = 2018,
            IdentityNumber = Guid.NewGuid().ToString("N")[..11],
            Nationality = "TR",
            TokenVersion = 0
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            PrincipalType = AuthPrincipalType.Customer,
            PrincipalId = customer.Id,
            RefreshTokenHash = $"test-refresh-{Guid.NewGuid():N}",
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            CreatedByIp = "127.0.0.1",
            UserAgent = "integration-tests"
        };

        dbContext.AuthSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return jwtTokenService.CreateCustomerAccessToken(customer, session.Id, out _);
    }
}
