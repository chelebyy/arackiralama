using FluentAssertions;
using RentACar.API.Services;
using RentACar.Core.Entities;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class VehicleBookingPolicyTests
{
    private static Office Office(int notice = 60) => new()
    {
        OperatingPolicy = new OfficeOperatingPolicy
        {
            MinimumNoticeMinutes = notice, PreparationMinutes = 90,
            PickupWindows = [new() { Day = DayOfWeek.Monday, StartMinute = 540, EndMinute = 1080 }],
            ReturnWindows = [new() { Day = DayOfWeek.Monday, StartMinute = 540, EndMinute = 1080 }]
        }
    };

    [Theory]
    [InlineData(5, 59, false)]
    [InlineData(6, 0, true)]
    [InlineData(14, 59, true)]
    [InlineData(15, 0, false)]
    public void OperatingWindows_UseTurkeyTimeAndExclusiveClosing(int hour, int minute, bool accepted)
    {
        var from = new DateTime(2026, 10, 5, hour, minute, 0, DateTimeKind.Utc);
        Action act = () => VehicleBookingService.ValidateItinerary(Office(), Office(), from, from.AddDays(7), from.AddDays(-1));
        if (accepted) act.Should().NotThrow(); else act.Should().Throw<ReservationQuoteConflictException>();
    }

    [Fact]
    public void MissingConfigurationAndHolidays_FailClosed()
    {
        var from = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        Action missing = () => VehicleBookingService.ValidateItinerary(new Office(), Office(), from, from.AddDays(7), from.AddDays(-1));
        missing.Should().Throw<ReservationQuoteConflictException>();
        var office = Office();
        office.OperatingPolicy!.ClosedDates = [new DateOnly(2026, 10, 5)];
        Action closed = () => VehicleBookingService.ValidateItinerary(office, Office(), from, from.AddDays(7), from.AddDays(-1));
        closed.Should().Throw<ReservationQuoteConflictException>();
    }

    [Fact]
    public void MinimumNotice_IsInclusiveAtBoundary()
    {
        var from = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        Action valid = () => VehicleBookingService.ValidateItinerary(Office(), Office(), from, from.AddDays(7), from.AddHours(-1));
        valid.Should().NotThrow();
        Action shortNotice = () => VehicleBookingService.ValidateItinerary(Office(), Office(), from, from.AddDays(7), from.AddMinutes(-59));
        shortNotice.Should().Throw<ReservationQuoteConflictException>();
    }

    [Fact]
    public void InvalidPolicyBoundsAndOverlappingWindows_AreRejected()
    {
        var policy = Office().OperatingPolicy!;
        policy.PreparationMinutes = -1;
        Action bounds = () => VehicleBookingService.ValidatePolicy(policy);
        bounds.Should().Throw<ArgumentException>();
        policy.PreparationMinutes = 60;
        policy.PickupWindows.Add(new() { Day = DayOfWeek.Monday, StartMinute = 1000, EndMinute = 1100 });
        bounds.Should().Throw<ArgumentException>();
    }
}
