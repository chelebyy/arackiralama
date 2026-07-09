using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    public async Task LateInventoryBackfill_AssignsAndActivatesBuiltInsOnlyOnce()
    {
        await WithDbContextAsync(async dbContext =>
        {
            await dbContext.ReservationExtraOptionVehicleGroups.ExecuteDeleteAsync();
            await dbContext.ReservationExtraOptions.ExecuteUpdateAsync(setters => setters.SetProperty(option => option.IsActive, false));
            dbContext.VehicleGroups.Add(CreateVehicleGroup());
            await dbContext.SaveChangesAsync();
            return true;
        });

        await Services.ApplyReservationExtraOptionsBackfillAsync();
        var versionsAfterFirstBackfill = await WithDbContextAsync(dbContext =>
            dbContext.ReservationExtraOptions
                .OrderBy(option => option.Id)
                .Select(option => new { option.Id, option.Version, option.UpdatedAt })
                .ToListAsync());

        await Services.ApplyReservationExtraOptionsBackfillAsync();
        var versionsAfterSecondBackfill = await WithDbContextAsync(dbContext =>
            dbContext.ReservationExtraOptions
                .OrderBy(option => option.Id)
                .Select(option => new { option.Id, option.Version, option.UpdatedAt })
                .ToListAsync());

        var result = await WithDbContextAsync(async dbContext => new
        {
            OptionCount = await dbContext.ReservationExtraOptions.CountAsync(),
            GroupCount = await dbContext.VehicleGroups.CountAsync(),
            AssignmentCount = await dbContext.ReservationExtraOptionVehicleGroups.CountAsync(),
            ActiveCount = await dbContext.ReservationExtraOptions.CountAsync(option => option.IsActive)
        });

        result.AssignmentCount.Should().Be(result.OptionCount * result.GroupCount);
        result.ActiveCount.Should().Be(result.OptionCount);
        versionsAfterSecondBackfill.Should().BeEquivalentTo(versionsAfterFirstBackfill, options => options.WithStrictOrdering());
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

    private static VehicleGroup CreateVehicleGroup() => new()
    {
        NameTr = "Geç Grup",
        NameEn = "Late Group",
        NameDe = "Späte Gruppe",
        NameRu = "Поздняя группа",
        NameAr = "مجموعة متأخرة",
        DepositAmount = 2500m,
        MinAge = 21,
        MinLicenseYears = 2,
        IsActive = true
    };
}
