using FluentAssertions;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ReservationExtraPricingServiceTests
{
    [Fact]
    public async Task CalculateAsync_CalculatesPerDayPerRentalFreeAndQuantities()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var groupId = Guid.NewGuid();
        var perDay = CreateOption(groupId, "gps", 8m, ReservationExtraPricingMode.PerDay, 3, version: 12);
        var perRental = CreateOption(groupId, "additional_driver", 150m, ReservationExtraPricingMode.PerRental, 4, version: 18);
        var free = CreateOption(groupId, "wifi", 0m, ReservationExtraPricingMode.PerRental, 1, version: 24);
        context.AddRange(perDay, perRental, free);
        await context.SaveChangesAsync();

        var service = new ReservationExtraPricingService(context);
        var result = await service.CalculateAsync(groupId, "en", 5,
        [
            Input(perDay, 2),
            Input(perRental, 3),
            Input(free, 1)
        ]);

        result.Should().HaveCount(3);
        result.Single(item => item.Code == "gps").Total.Should().Be(80m);
        result.Single(item => item.Code == "additional_driver").Total.Should().Be(450m);
        result.Single(item => item.Code == "wifi").Total.Should().Be(0m);
        result.Should().OnlyContain(item => item.Locale == "en" && item.Name.EndsWith(" en"));
    }

    [Fact]
    public async Task CalculateAsync_RejectsDuplicateOptionIds()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var option = CreateOption(Guid.NewGuid(), "gps", 8m, ReservationExtraPricingMode.PerDay, 2, version: 1);
        context.Add(option);
        await context.SaveChangesAsync();
        var service = new ReservationExtraPricingService(context);

        var act = () => service.CalculateAsync(option.VehicleGroups.Single().VehicleGroupId, "tr", 2,
            [Input(option, 1), Input(option, 1)]);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public async Task CalculateAsync_RejectsMissingOptionAsQuoteConflict()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var service = new ReservationExtraPricingService(context);

        var act = () => service.CalculateAsync(Guid.NewGuid(), "tr", 2,
        [
            new SelectedReservationExtraInput
            {
                OptionId = Guid.NewGuid(),
                Quantity = 1,
                OptionVersion = 1
            }
        ]);

        await act.Should().ThrowAsync<ReservationQuoteConflictException>().WithMessage("*no longer exist*");
    }

    [Theory]
    [InlineData(false, false, 1, 1, 9, "not active")]
    [InlineData(true, true, 1, 1, 9, "not active")]
    [InlineData(true, false, 1, 2, 9, "maximum")]
    [InlineData(true, false, 1, 1, 8, "stale")]
    public async Task CalculateAsync_RejectsInvalidCurrentState(
        bool isActive,
        bool isArchived,
        int maxQuantity,
        int quantity,
        uint requestedVersion,
        string message)
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var groupId = Guid.NewGuid();
        var option = CreateOption(groupId, "gps", 8m, ReservationExtraPricingMode.PerDay, maxQuantity, version: 9);
        option.IsActive = isActive;
        option.IsArchived = isArchived;
        context.Add(option);
        await context.SaveChangesAsync();
        var service = new ReservationExtraPricingService(context);

        var act = () => service.CalculateAsync(groupId, "tr", 2,
        [
            new SelectedReservationExtraInput
            {
                OptionId = option.Id,
                Quantity = quantity,
                OptionVersion = requestedVersion
            }
        ]);

        await act.Should().ThrowAsync<ReservationQuoteConflictException>().WithMessage($"*{message}*");
    }

    [Fact]
    public async Task CalculateAsync_RejectsWrongVehicleGroup()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var option = CreateOption(Guid.NewGuid(), "gps", 8m, ReservationExtraPricingMode.PerDay, 1, version: 5);
        context.Add(option);
        await context.SaveChangesAsync();
        var service = new ReservationExtraPricingService(context);

        var act = () => service.CalculateAsync(Guid.NewGuid(), "tr", 2, [Input(option, 1)]);

        await act.Should().ThrowAsync<ReservationQuoteConflictException>().WithMessage("*vehicle group*");
    }

    [Fact]
    public async Task ValidateCurrentAvailabilityAsync_AllowsPriceAndVersionChange()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var groupId = Guid.NewGuid();
        var option = CreateOption(groupId, "gps", 8m, ReservationExtraPricingMode.PerDay, 2, version: 5);
        context.Add(option);
        await context.SaveChangesAsync();
        var service = new ReservationExtraPricingService(context);
        var quoted = await service.CalculateAsync(groupId, "tr", 2, [Input(option, 1)]);

        option.UnitPrice = 99m;
        option.Version = 6;
        await context.SaveChangesAsync();

        var act = () => service.ValidateCurrentAvailabilityAsync(groupId, quoted);
        await act.Should().NotThrowAsync();
        quoted.Single().Total.Should().Be(16m);
    }

    [Fact]
    public async Task ValidateCurrentAvailabilityAsync_RejectsReducedMaximum()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var groupId = Guid.NewGuid();
        var option = CreateOption(groupId, "child_seat", 75m, ReservationExtraPricingMode.PerDay, 3, version: 5);
        context.Add(option);
        await context.SaveChangesAsync();
        var service = new ReservationExtraPricingService(context);
        var quoted = await service.CalculateAsync(groupId, "tr", 2, [Input(option, 2)]);

        option.MaxQuantity = 1;
        await context.SaveChangesAsync();

        var act = () => service.ValidateCurrentAvailabilityAsync(groupId, quoted);
        await act.Should().ThrowAsync<ReservationQuoteConflictException>();
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("archived")]
    [InlineData("pricing-mode")]
    [InlineData("group-removed")]
    public async Task ValidateCurrentAvailabilityAsync_RejectsStructuralChanges(string change)
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var groupId = Guid.NewGuid();
        var option = CreateOption(groupId, "gps", 8m, ReservationExtraPricingMode.PerDay, 2, version: 5);
        context.Add(option);
        await context.SaveChangesAsync();
        var service = new ReservationExtraPricingService(context);
        var quoted = await service.CalculateAsync(groupId, "tr", 2, [Input(option, 1)]);

        switch (change)
        {
            case "inactive":
                option.IsActive = false;
                break;
            case "archived":
                option.IsArchived = true;
                break;
            case "pricing-mode":
                option.PricingMode = ReservationExtraPricingMode.PerRental;
                break;
            case "group-removed":
                context.ReservationExtraOptionVehicleGroups.Remove(option.VehicleGroups.Single());
                break;
        }
        await context.SaveChangesAsync();

        var act = () => service.ValidateCurrentAvailabilityAsync(groupId, quoted);
        await act.Should().ThrowAsync<ReservationQuoteConflictException>();
    }

    private static ReservationExtraOption CreateOption(
        Guid groupId,
        string code,
        decimal price,
        ReservationExtraPricingMode mode,
        int maxQuantity,
        uint version)
    {
        var option = new ReservationExtraOption
        {
            Code = code,
            UnitPrice = price,
            PricingMode = mode,
            MaxQuantity = maxQuantity,
            IconKey = "navigation",
            IsActive = true,
            Version = version
        };
        option.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup
        {
            OptionId = option.Id,
            VehicleGroupId = groupId
        });
        foreach (var locale in new[] { "tr", "en", "de", "ru", "ar" })
        {
            option.Translations.Add(new ReservationExtraOptionTranslation
            {
                OptionId = option.Id,
                Locale = locale,
                Name = $"{code} {locale}",
                Description = $"{code} description {locale}"
            });
        }
        return option;
    }

    private static SelectedReservationExtraInput Input(ReservationExtraOption option, int quantity) => new()
    {
        OptionId = option.Id,
        Quantity = quantity,
        OptionVersion = option.Version
    };
}
