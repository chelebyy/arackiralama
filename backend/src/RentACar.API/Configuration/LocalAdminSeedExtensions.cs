using Microsoft.EntityFrameworkCore;
using RentACar.API.Authentication;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Configuration;

public static class LocalAdminSeedExtensions
{
    private static readonly Guid SeedAdminId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");
    private const string DefaultEmail = "integration-admin@rentacar.test";
    private const string DefaultFullName = "Integration Test Admin";

    public static async Task ApplyLocalAdminSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LocalAdminSeed");
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

        if (!configuration.GetValue<bool>("LocalAdminSeed:Enabled"))
        {
            logger.LogInformation("Local admin seed is disabled.");
            return;
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning("Local admin seed is enabled outside Development and was skipped.");
            return;
        }

        var password = configuration["LocalAdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Local admin seed skipped because password is empty.");
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<IApplicationDbContext>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedEmail = AdminUser.NormalizeEmail(DefaultEmail);
        var admin = await dbContext.AdminUsers
            .FirstOrDefaultAsync(
                user => user.Id == SeedAdminId || user.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (admin is null)
        {
            dbContext.AdminUsers.Add(new AdminUser
            {
                Id = SeedAdminId,
                Email = DefaultEmail,
                PasswordHash = passwordHasher.HashPassword(password),
                FullName = DefaultFullName,
                Role = AuthRoleNames.SuperAdmin,
                IsActive = true,
                FailedLoginCount = 0,
                LockoutEndUtc = null
            });
        }
        else
        {
            admin.Email = DefaultEmail;
            admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? DefaultFullName : admin.FullName;
            admin.PasswordHash = passwordHasher.HashPassword(password);
            admin.Role = AuthRoleNames.SuperAdmin;
            admin.IsActive = true;
            admin.FailedLoginCount = 0;
            admin.LockoutEndUtc = null;
            admin.TokenVersion += 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Local SuperAdmin seed applied.");
    }
}
