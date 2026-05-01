using Microsoft.EntityFrameworkCore;
using RentACar.API.Authentication;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Security;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Seeds reusable integration test data into the PostgreSQL test database.
/// </summary>
public static class TestDataSeeder
{
    public static readonly Guid OfficeOneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid OfficeTwoId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    public static readonly Guid GroupOneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
    public static readonly Guid GroupTwoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    public static readonly Guid AdminUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");

    private const string SeedAdminEmail = "integration-admin@rentacar.test";
    private const string SeedAdminPassword = "IntegrationTestPassword123!";

    /// <summary>
    /// Ensures baseline offices, vehicle groups, vehicles, and an admin user exist.
    /// </summary>
    public static async Task SeedAsync(RentACarDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await EnsureOfficesAsync(dbContext, cancellationToken);
        await EnsureVehicleGroupsAsync(dbContext, cancellationToken);
        await EnsureVehiclesAsync(dbContext, cancellationToken);
        await EnsurePricingRulesAsync(dbContext, cancellationToken);
        await EnsureAdminUserAsync(dbContext, cancellationToken);
        await EnsureFeatureFlagsAsync(dbContext, cancellationToken);
    }

    /// <summary>
    /// Gets the seeded admin email.
    /// </summary>
    public static string GetSeedAdminEmail() => SeedAdminEmail;

    /// <summary>
    /// Gets the seeded admin password.
    /// </summary>
    public static string GetSeedAdminPassword() => SeedAdminPassword;

    private static async Task EnsureOfficesAsync(RentACarDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Offices.AnyAsync(o => o.Id == OfficeOneId, cancellationToken))
        {
            return;
        }

        var offices = new[]
        {
            new Office
            {
                Id = OfficeOneId,
                Name = "Integration Test Downtown",
                Address = "Ataturk Boulevard No:10, Alanya",
                Phone = "+90 242 111 11 11",
                IsAirport = false,
                OpeningHours = "08:00-22:00"
            },
            new Office
            {
                Id = OfficeTwoId,
                Name = "Integration Test Airport",
                Address = "Gazipasa Airport Terminal",
                Phone = "+90 242 222 22 22",
                IsAirport = true,
                OpeningHours = "24/7"
            }
        };

        await dbContext.Offices.AddRangeAsync(offices, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureVehicleGroupsAsync(RentACarDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.VehicleGroups.AnyAsync(g => g.Id == GroupOneId, cancellationToken))
        {
            return;
        }

        var groups = new[]
        {
            new VehicleGroup
            {
                Id = GroupOneId,
                NameTr = "Test Ekonomi",
                NameEn = "Test Economy",
                NameRu = "Test Economy",
                NameAr = "Test Economy",
                NameDe = "Test Economy",
                DepositAmount = 1500m,
                MinAge = 21,
                MinLicenseYears = 2,
                Features = ["AirConditioning", "AutomaticTransmission"]
            },
            new VehicleGroup
            {
                Id = GroupTwoId,
                NameTr = "Test SUV",
                NameEn = "Test SUV",
                NameRu = "Test SUV",
                NameAr = "Test SUV",
                NameDe = "Test SUV",
                DepositAmount = 3000m,
                MinAge = 25,
                MinLicenseYears = 3,
                Features = ["AirConditioning", "AutomaticTransmission", "BackupCamera"]
            }
        };

        await dbContext.VehicleGroups.AddRangeAsync(groups, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureVehiclesAsync(RentACarDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Vehicles.AnyAsync(v => v.Plate == "IT-1001", cancellationToken))
        {
            return;
        }

        var vehicles = new[]
        {
            new Vehicle
            {
                Id = GroupOneId,
                Plate = "IT-1001",
                Brand = "Renault",
                Model = "Clio",
                Year = 2024,
                Color = "White",
                GroupId = GroupOneId,
                OfficeId = OfficeOneId,
                Status = VehicleStatus.Available
            },
            new Vehicle
            {
                Plate = "IT-1002",
                Brand = "Fiat",
                Model = "Egea",
                Year = 2024,
                Color = "Gray",
                GroupId = GroupOneId,
                OfficeId = OfficeTwoId,
                Status = VehicleStatus.Available
            },
            new Vehicle
            {
                Id = GroupTwoId,
                Plate = "IT-2001",
                Brand = "Dacia",
                Model = "Duster",
                Year = 2025,
                Color = "Black",
                GroupId = GroupTwoId,
                OfficeId = OfficeOneId,
                Status = VehicleStatus.Available
            },
            new Vehicle
            {
                Plate = "IT-2002",
                Brand = "Nissan",
                Model = "Qashqai",
                Year = 2025,
                Color = "Blue",
                GroupId = GroupTwoId,
                OfficeId = OfficeTwoId,
                Status = VehicleStatus.Available
            }
        };

        await dbContext.Vehicles.AddRangeAsync(vehicles, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsurePricingRulesAsync(RentACarDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.PricingRules.AnyAsync(r => r.VehicleGroupId == GroupOneId, cancellationToken))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rules = new[]
        {
            new PricingRule
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                VehicleGroupId = GroupOneId,
                StartDate = today.AddYears(-1),
                EndDate = today.AddYears(1),
                DailyPrice = 1000m,
                Multiplier = 1m,
                WeekdayMultiplier = 1m,
                WeekendMultiplier = 1m,
                CalculationType = "fixed",
                Priority = 1
            },
            new PricingRule
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd2"),
                VehicleGroupId = GroupTwoId,
                StartDate = today.AddYears(-1),
                EndDate = today.AddYears(1),
                DailyPrice = 1500m,
                Multiplier = 1m,
                WeekdayMultiplier = 1m,
                WeekendMultiplier = 1m,
                CalculationType = "fixed",
                Priority = 1
            }
        };

        await dbContext.PricingRules.AddRangeAsync(rules, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdminUserAsync(RentACarDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.AdminUsers.AnyAsync(admin => admin.NormalizedEmail == AdminUser.NormalizeEmail(SeedAdminEmail), cancellationToken))
        {
            return;
        }

        var passwordHasher = new BcryptPasswordHasher();

        dbContext.AdminUsers.Add(new AdminUser
        {
            Id = AdminUserId,
            Email = SeedAdminEmail,
            PasswordHash = passwordHasher.HashPassword(SeedAdminPassword),
            FullName = "Integration Test Admin",
            Role = AuthRoleNames.Admin,
            IsActive = true,
            TokenVersion = 0
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureFeatureFlagsAsync(RentACarDbContext dbContext, CancellationToken cancellationToken)
    {
        var onlinePaymentFlag = await dbContext.FeatureFlags
            .FirstOrDefaultAsync(f => f.Name == "EnableOnlinePayment", cancellationToken);

        if (onlinePaymentFlag is not null && !onlinePaymentFlag.Enabled)
        {
            onlinePaymentFlag.Enabled = true;
            onlinePaymentFlag.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

}
