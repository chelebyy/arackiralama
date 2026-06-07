using Microsoft.EntityFrameworkCore;
using RentACar.API.Authentication;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Configuration;

public static class LocalAdminSeedExtensions
{
    private const string DefaultEmail = "integration-admin@rentacar.test";
    private const string DefaultPassword = "IntegrationTestPassword123!";
    private const string DefaultFullName = "Integration Test Admin";

    public static async Task ApplyLocalAdminSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LocalAdminSeed");

        if (!configuration.GetValue<bool>("LocalAdminSeed:Enabled"))
        {
            logger.LogInformation("Local admin seed is disabled.");
            return;
        }

        var email = configuration["LocalAdminSeed:Email"] ?? DefaultEmail;
        var password = configuration["LocalAdminSeed:Password"] ?? DefaultPassword;
        var fullName = configuration["LocalAdminSeed:FullName"] ?? DefaultFullName;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Local admin seed skipped because email or password is empty.");
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<IApplicationDbContext>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedEmail = AdminUser.NormalizeEmail(email);
        var admin = await dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (admin is null)
        {
            dbContext.AdminUsers.Add(new AdminUser
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
                Email = email,
                PasswordHash = passwordHasher.HashPassword(password),
                FullName = fullName,
                Role = AuthRoleNames.SuperAdmin,
                IsActive = true,
                FailedLoginCount = 0,
                LockoutEndUtc = null
            });
        }
        else
        {
            admin.Email = email;
            admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? fullName : admin.FullName;
            admin.PasswordHash = passwordHasher.HashPassword(password);
            admin.Role = AuthRoleNames.SuperAdmin;
            admin.IsActive = true;
            admin.FailedLoginCount = 0;
            admin.LockoutEndUtc = null;
            admin.TokenVersion += 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Local SuperAdmin seed applied for {Email}.", email);
    }
}
