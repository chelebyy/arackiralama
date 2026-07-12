using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.ReservationExtraOptions;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using Xunit;

namespace RentACar.ApiIntegrationTests.Database;

public sealed class ReservationExtraOptionDatabaseTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task SeedData_ContainsBuiltInsTranslationsAndGroupAssignments()
    {
        var result = await WithDbContextAsync(async dbContext => new
        {
            Codes = await dbContext.ReservationExtraOptions.OrderBy(option => option.SortOrder).Select(option => option.Code).ToListAsync(),
            TranslationCount = await dbContext.ReservationExtraOptionTranslations.CountAsync(),
            AssignmentCount = await dbContext.ReservationExtraOptionVehicleGroups.CountAsync(),
            AssignmentCounts = await dbContext.ReservationExtraOptionVehicleGroups
                .GroupBy(assignment => assignment.OptionId)
                .Select(assignments => assignments.Count())
                .ToListAsync(),
            ActiveCount = await dbContext.ReservationExtraOptions.CountAsync(option => option.IsActive)
        });

        result.Codes.Should().Equal("child_seat", "additional_driver", "gps", "wifi");
        result.TranslationCount.Should().Be(20);
        result.AssignmentCount.Should().Be(8);
        result.AssignmentCounts.Should().OnlyContain(count => count == 2);
        result.ActiveCount.Should().Be(4);
    }

    [Fact]
    public async Task AdminDisabledBuiltIns_RemainDisabledAndUnassigned()
    {
        await WithDbContextAsync(async dbContext =>
        {
            await dbContext.ReservationExtraOptionVehicleGroups.ExecuteDeleteAsync();
            await dbContext.ReservationExtraOptions.ExecuteUpdateAsync(setters => setters.SetProperty(option => option.IsActive, false));
            return true;
        });

        var result = await WithDbContextAsync(async dbContext => new
        {
            OptionCount = await dbContext.ReservationExtraOptions.CountAsync(),
            AssignmentCount = await dbContext.ReservationExtraOptionVehicleGroups.CountAsync(),
            ActiveCount = await dbContext.ReservationExtraOptions.CountAsync(option => option.IsActive)
        });

        result.OptionCount.Should().Be(4);
        result.AssignmentCount.Should().Be(0);
        result.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task UsedOption_CannotBeHardDeleted()
    {
        var ids = await CreateReservationWithSelectedExtraAsync();

        await WithDbContextAsync(async dbContext =>
        {
            var option = await dbContext.ReservationExtraOptions.SingleAsync(item => item.Id == ids.OptionId);
            dbContext.ReservationExtraOptions.Remove(option);

            var act = () => dbContext.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
            return true;
        });
    }

    [Fact]
    public async Task DuplicateReservationOptionSnapshot_IsRejected()
    {
        var reservationId = await CreateReservationAsync();
        var optionId = await WithDbContextAsync(dbContext =>
            dbContext.ReservationExtraOptions.Select(option => option.Id).FirstAsync());

        await WithDbContextAsync(async dbContext =>
        {
            dbContext.ReservationSelectedExtras.AddRange(
                CreateSelectedExtra(reservationId, optionId),
                CreateSelectedExtra(reservationId, optionId));

            var act = () => dbContext.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
            return true;
        });
    }

    [Fact]
    public async Task DuplicateQuoteId_IsRejected()
    {
        var quoteId = Guid.NewGuid();
        var firstReservationId = await CreateReservationAsync(quoteId);
        firstReservationId.Should().NotBeEmpty();

        await WithDbContextAsync(async dbContext =>
        {
            dbContext.Reservations.Add(await CreateReservationEntityAsync(dbContext, quoteId));
            var act = () => dbContext.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
            return true;
        });
    }

    [Fact]
    public async Task CatalogService_TwoContextsRejectStaleWrite()
    {
        using var firstScope = Services.CreateScope();
        using var secondScope = Services.CreateScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var firstService = firstScope.ServiceProvider.GetRequiredService<IReservationExtraOptionCatalogService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<IReservationExtraOptionCatalogService>();
        var optionId = await firstDbContext.ReservationExtraOptions.Select(option => option.Id).FirstAsync();

        var firstOption = await firstDbContext.ReservationExtraOptions
            .Include(option => option.Translations)
            .Include(option => option.VehicleGroups)
            .SingleAsync(option => option.Id == optionId);
        var staleOption = await secondDbContext.ReservationExtraOptions
            .Include(option => option.Translations)
            .Include(option => option.VehicleGroups)
            .SingleAsync(option => option.Id == optionId);

        await firstService.UpdateAsync(
            optionId,
            UpdateRequest(firstOption, firstOption.UnitPrice + 1),
            AuditContext(),
            CancellationToken.None);

        var staleWrite = () => secondService.UpdateAsync(
            optionId,
            UpdateRequest(staleOption, staleOption.UnitPrice + 2),
            AuditContext(),
            CancellationToken.None);

        await staleWrite.Should().ThrowAsync<ReservationExtraOptionConcurrencyException>();
    }

    [Fact]
    public async Task CatalogService_ChildMutationAdvancesParentVersion()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IReservationExtraOptionCatalogService>();
        var option = await dbContext.ReservationExtraOptions
            .Include(item => item.Translations)
            .Include(item => item.VehicleGroups)
            .OrderBy(item => item.SortOrder)
            .FirstAsync();
        var originalVersion = option.Version;
        var translations = option.Translations
            .Select(item => new ReservationExtraOptionTranslationDto(
                item.Locale,
                item.Name,
                item.Locale == "en" ? $"{item.Description} updated" : item.Description))
            .ToList();

        await service.UpdateAsync(
            option.Id,
            new UpdateReservationExtraOptionRequest(
                option.Version,
                option.UnitPrice,
                option.PricingMode,
                option.MaxQuantity,
                option.IconKey,
                option.SortOrder,
                option.VehicleGroups.Select(item => item.VehicleGroupId).ToList(),
                translations),
            AuditContext(),
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var updatedVersion = await dbContext.ReservationExtraOptions
            .Where(item => item.Id == option.Id)
            .Select(item => item.Version)
            .SingleAsync();
        updatedVersion.Should().NotBe(originalVersion);
    }

    [Fact]
    public async Task CatalogService_ConcurrentReferenceDuringHardDeleteArchivesOption()
    {
        var reservationId = await CreateReservationAsync();
        var setup = await WithDbContextAsync(async dbContext =>
        {
            var option = new ReservationExtraOption
            {
                Code = $"race-{Guid.NewGuid():N}",
                UnitPrice = 10m,
                PricingMode = ReservationExtraPricingMode.PerRental,
                MaxQuantity = 1,
                IconKey = "users",
                SortOrder = 100,
                IsActive = false,
                IsArchived = false
            };
            dbContext.ReservationExtraOptions.Add(option);
            await dbContext.SaveChangesAsync();
            return new
            {
                option.Id,
                option.Version,
                ConnectionString = dbContext.Database.GetConnectionString()!
            };
        });

        var options = new DbContextOptionsBuilder<RentACarDbContext>()
            .UseNpgsql(setup.ConnectionString)
            .Options;
        await using var raceContext = new HardDeleteRaceDbContext(options, async () =>
        {
            await WithDbContextAsync(async dbContext =>
            {
                dbContext.ReservationSelectedExtras.Add(CreateSelectedExtra(reservationId, setup.Id));
                await dbContext.SaveChangesAsync();
                return true;
            });
        });
        var service = new ReservationExtraOptionCatalogService(raceContext);

        var result = await service.DeleteAsync(
            setup.Id,
            setup.Version,
            AuditContext(),
            CancellationToken.None);

        result.Disposition.Should().Be("archived");
        var archived = await WithDbContextAsync(dbContext =>
            dbContext.ReservationExtraOptions.AsNoTracking().SingleAsync(option => option.Id == setup.Id));
        archived.IsArchived.Should().BeTrue();
        archived.IsActive.Should().BeFalse();
    }

    private async Task<(Guid ReservationId, Guid OptionId)> CreateReservationWithSelectedExtraAsync()
    {
        var reservationId = await CreateReservationAsync();
        var optionId = await WithDbContextAsync(dbContext =>
            dbContext.ReservationExtraOptions.Select(option => option.Id).FirstAsync());

        await WithDbContextAsync(async dbContext =>
        {
            dbContext.ReservationSelectedExtras.Add(CreateSelectedExtra(reservationId, optionId));
            await dbContext.SaveChangesAsync();
            return true;
        });

        return (reservationId, optionId);
    }

    private async Task<Guid> CreateReservationAsync(Guid? quoteId = null) =>
        await WithDbContextAsync(async dbContext =>
        {
            var reservation = await CreateReservationEntityAsync(dbContext, quoteId);
            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync();
            return reservation.Id;
        });

    private static async Task<Reservation> CreateReservationEntityAsync(RentACarDbContext dbContext, Guid? quoteId)
    {
        var customer = new Customer
        {
            Email = $"extras-{Guid.NewGuid():N}@rentacar.test",
            FullName = "Extras Customer",
            Phone = "+90 555 000 00 15",
            IdentityNumber = Guid.NewGuid().ToString("N")[..11],
            Nationality = "TR",
            LicenseYear = 2018
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var vehicleId = await dbContext.Vehicles.Select(vehicle => vehicle.Id).FirstAsync();
        var officeId = await dbContext.Offices.Select(office => office.Id).FirstAsync();
        return new Reservation
        {
            PublicCode = $"EX{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CustomerId = customer.Id,
            VehicleId = vehicleId,
            PickupOfficeId = officeId,
            ReturnOfficeId = officeId,
            PickupDateTime = DateTime.UtcNow.Date.AddDays(90),
            ReturnDateTime = DateTime.UtcNow.Date.AddDays(93),
            Status = ReservationStatus.Draft,
            TotalAmount = 1000m,
            QuoteId = quoteId
        };
    }

    private static ReservationSelectedExtra CreateSelectedExtra(Guid reservationId, Guid optionId) => new()
    {
        ReservationId = reservationId,
        ExtraOptionId = optionId,
        OptionVersionSnapshot = 1,
        Locale = "tr",
        OptionCodeSnapshot = "snapshot_option",
        NameSnapshot = "Snapshot Option",
        DescriptionSnapshot = "Snapshot description",
        UnitPriceSnapshot = 75m,
        PricingModeSnapshot = ReservationExtraPricingMode.PerDay,
        Quantity = 1,
        RentalDaysSnapshot = 3,
        TotalPriceSnapshot = 225m,
        Currency = "TRY"
    };

    private static UpdateReservationExtraOptionRequest UpdateRequest(
        ReservationExtraOption option,
        decimal unitPrice) => new(
        option.Version,
        unitPrice,
        option.PricingMode,
        option.MaxQuantity,
        option.IconKey,
        option.SortOrder,
        option.VehicleGroups.Select(item => item.VehicleGroupId).ToList(),
        option.Translations
            .Select(item => new ReservationExtraOptionTranslationDto(item.Locale, item.Name, item.Description))
            .ToList());

    private static ReservationExtraOptionAuditContext AuditContext() => new("integration-admin", "127.0.0.1");

    private sealed class HardDeleteRaceDbContext(
        DbContextOptions<RentACarDbContext> options,
        Func<Task> beforeFirstSave) : RentACarDbContext(options)
    {
        private Func<Task>? _beforeFirstSave = beforeFirstSave;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_beforeFirstSave is not null)
            {
                var callback = _beforeFirstSave;
                _beforeFirstSave = null;
                await callback();
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
