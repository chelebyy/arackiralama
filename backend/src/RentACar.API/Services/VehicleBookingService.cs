using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed record VehicleBookingOffer(
    PriceBreakdownDto Pricing, IReadOnlyList<ReservationQuotedExtraV1> Extras,
    ReservationBookingConditions Conditions);

public sealed class VehicleBookingService(
    IApplicationDbContext db, PricingService pricing, ReservationExtraPricingService extras)
{
    public async Task<VehicleBookingOffer> CalculateAsync(
        CreateReservationQuoteRequest request, CancellationToken cancellationToken = default, Guid? excludeReservationId = null)
    {
        request = request with
        {
            PickupDateTimeUtc = request.PickupDateTimeUtc.Kind == DateTimeKind.Local ? request.PickupDateTimeUtc.ToUniversalTime() : DateTime.SpecifyKind(request.PickupDateTimeUtc, DateTimeKind.Utc),
            ReturnDateTimeUtc = request.ReturnDateTimeUtc.Kind == DateTimeKind.Local ? request.ReturnDateTimeUtc.ToUniversalTime() : DateTime.SpecifyKind(request.ReturnDateTimeUtc, DateTimeKind.Utc)
        };
        if (!request.VehicleId.HasValue || request.VehicleId == Guid.Empty ||
            request.PickupDateTimeUtc >= request.ReturnDateTimeUtc ||
            request.ReturnDateTimeUtc - request.PickupDateTimeUtc > TimeSpan.FromDays(366))
            throw new ArgumentException("A vehicle and a valid rental interval of at most 366 days are required.");

        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.VehicleId, cancellationToken);
        if (vehicle is null || vehicle.Status != VehicleStatus.Available ||
            vehicle.OfficeId != request.PickupOfficeId ||
            (request.VehicleGroupId != Guid.Empty && vehicle.GroupId != request.VehicleGroupId))
            throw new ReservationQuoteConflictException("Selected vehicle is unavailable for this itinerary.");

        var terms = vehicle.RentalTerms;
        if (terms is null)
            throw new ReservationQuoteConflictException("Vehicle rental terms have not been configured.");
        ValidateTerms(terms);
        var returnId = request.ReturnOfficeId == Guid.Empty ? request.PickupOfficeId : request.ReturnOfficeId;
        var pickup = await db.Offices.AsNoTracking().SingleOrDefaultAsync(o => o.Id == request.PickupOfficeId, cancellationToken);
        var dropoff = await db.Offices.AsNoTracking().SingleOrDefaultAsync(o => o.Id == returnId, cancellationToken);
        ValidateItinerary(pickup, dropoff, request.PickupDateTimeUtc, request.ReturnDateTimeUtc, DateTime.UtcNow);
        var preparation = dropoff!.OperatingPolicy!.PreparationMinutes!.Value;
        var occupiedUntil = request.ReturnDateTimeUtc.AddMinutes(preparation);
        if (await db.Reservations.AsNoTracking().AnyAsync(r => r.VehicleId == vehicle.Id && r.Id != excludeReservationId &&
                ReservationStatusGroups.StockBlocking.Contains(r.Status) &&
                r.PickupDateTime < occupiedUntil &&
                (r.OccupiedUntilUtc ?? r.ReturnDateTime) > request.PickupDateTimeUtc, cancellationToken))
            throw new ReservationQuoteConflictException("Selected vehicle is unavailable for this itinerary.");
        if (request.DriverAge.HasValue && request.DriverAge < terms.MinAge)
            throw new ReservationQuoteConflictException("Driver does not meet this vehicle's minimum age.");

        var breakdown = await pricing.CalculateForVehicleAsync(vehicle, request.PickupOfficeId, returnId,
            request.PickupDateTimeUtc, request.ReturnDateTimeUtc, request.CampaignCode, request.DriverAge,
            request.FullCoverageWaiver, cancellationToken)
            ?? throw new ReservationQuoteConflictException("No vehicle rate exists for the selected dates.");
        if (!string.IsNullOrWhiteSpace(request.CampaignCode) && breakdown.AppliedCampaignCode is null)
            throw new ReservationQuoteConflictException("Campaign code is invalid or expired.");
        var selections = await extras.CalculateForVehicleAsync(vehicle, request.Locale, breakdown.RentalDays,
            request.SelectedExtras ?? [], cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            vehicle.Id, vehicle.GroupId, vehicle.OfficeId, terms,
            PickupPolicy = pickup!.OperatingPolicy, ReturnPolicy = dropoff.OperatingPolicy,
            Pricing = breakdown, Extras = selections
        })));
        return new VehicleBookingOffer(breakdown, selections, new ReservationBookingConditions
        {
            MinAge = terms.MinAge!.Value, MinLicenseYears = terms.MinLicenseYears!.Value,
            PreparationMinutes = preparation, PolicyFingerprint = fingerprint, PaymentAtPickup = true
        });
    }

    public async Task ValidateQuoteAsync(ReservationQuoteV1 quote, CreateReservationRequest request,
        CancellationToken cancellationToken, Guid? excludeReservationId = null)
    {
        if (db is DbContext context && context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM offices WHERE id IN ({quote.PickupOfficeId}, {quote.ReturnOfficeId}) ORDER BY id FOR SHARE", cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM vehicles WHERE id = {quote.VehicleId} FOR UPDATE", cancellationToken);
            foreach (var optionId in quote.SelectedExtras.Select(e => e.ExtraOptionId).Order())
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM reservation_extra_options WHERE id = {optionId} FOR SHARE", cancellationToken);
            if (quote.PricingSnapshot.CampaignId is { } campaignId)
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM campaigns WHERE id = {campaignId} FOR SHARE", cancellationToken);
        }
        var offer = await CalculateAsync(new CreateReservationQuoteRequest
        {
            VehicleId = quote.VehicleId, VehicleGroupId = quote.VehicleGroupId,
            PickupOfficeId = quote.PickupOfficeId, ReturnOfficeId = quote.ReturnOfficeId,
            PickupDateTimeUtc = quote.PickupDateTimeUtc, ReturnDateTimeUtc = quote.ReturnDateTimeUtc,
            CampaignCode = quote.CampaignCode, DriverAge = quote.DriverAge,
            FullCoverageWaiver = quote.FullCoverageWaiver, Locale = quote.Locale,
            SelectedExtras = quote.SelectedExtras.Select(item => new SelectedReservationExtraInput
            { OptionId = item.ExtraOptionId, OptionVersion = item.OptionVersion, Quantity = item.Quantity }).ToArray()
        }, cancellationToken, excludeReservationId);
        if (offer.Conditions.PolicyFingerprint != quote.PricingSnapshot.BookingConditions?.PolicyFingerprint)
            throw new ReservationQuoteConflictException("Price or rental conditions changed. Request a new quote.");
        var birth = request.Driver?.DateOfBirth ?? request.Customer.DateOfBirth;
        var license = request.Driver?.LicenseIssueDate ?? request.Customer.DriverLicenseIssueDate;
        var pickup = RentalCalendar.TurkeyDate(quote.PickupDateTimeUtc);
        if (birth is null || license is null)
            throw new ArgumentException("Driver birth date and license issue date are required.");
        var birthDate = DateOnly.FromDateTime(birth.Value);
        var licenseDate = DateOnly.FromDateTime(license.Value);
        var age = pickup.Year - birthDate.Year;
        if (birthDate.AddYears(age) > pickup) age--;
        if (age != quote.DriverAge || age < offer.Conditions.MinAge || licenseDate > pickup ||
            licenseDate.AddYears(offer.Conditions.MinLicenseYears) > pickup)
            throw new ReservationQuoteConflictException("Driver does not meet the quoted rental conditions.");
        if (request.Driver?.LicenseExpiryDate is { } expiry && DateOnly.FromDateTime(expiry) < RentalCalendar.TurkeyDate(quote.ReturnDateTimeUtc))
            throw new ReservationQuoteConflictException("Driver license expires before the rental ends.");
    }

    public Task ValidateDraftForHoldAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var snapshot = reservation.PricingSnapshot!;
        if (snapshot.ExpiresAtUtc <= DateTime.UtcNow)
            throw new ReservationQuoteConflictException("Reservation quote has expired. Request a new quote.");
        var birth = reservation.DriverDateOfBirth;
        var pickup = RentalCalendar.TurkeyDate(reservation.PickupDateTime);
        var age = birth.HasValue ? pickup.Year - birth.Value.Year : (int?)null;
        if (birth.HasValue && DateOnly.FromDateTime(birth.Value).AddYears(age!.Value) > pickup) age--;
        var quote = new ReservationQuoteV1
        {
            VehicleId = reservation.VehicleId, VehicleGroupId = reservation.Vehicle?.GroupId ?? Guid.Empty,
            PickupOfficeId = reservation.PickupOfficeId, ReturnOfficeId = reservation.ReturnOfficeId,
            PickupDateTimeUtc = reservation.PickupDateTime, ReturnDateTimeUtc = reservation.ReturnDateTime,
            DriverAge = age, CampaignCode = snapshot.CampaignCode, FullCoverageWaiver = snapshot.CoverageWaiverFee > 0,
            Locale = reservation.SelectedExtras.FirstOrDefault()?.Locale ?? "tr", PricingSnapshot = snapshot,
            SelectedExtras = reservation.SelectedExtras.Select(e => new ReservationQuotedExtraV1
            { ExtraOptionId = e.ExtraOptionId, OptionVersion = e.OptionVersionSnapshot, Quantity = e.Quantity }).ToList()
        };
        return ValidateQuoteAsync(quote, new CreateReservationRequest
        {
            Driver = new DriverInfoRequest { DateOfBirth = reservation.DriverDateOfBirth,
                LicenseIssueDate = reservation.DriverLicenseIssueDate, LicenseExpiryDate = reservation.DriverLicenseExpiryDate }
        }, cancellationToken, reservation.Id);
    }

    public static void ValidateTerms(VehicleRentalTerms terms)
    {
        if (terms.DepositAmount is null or < 0 or > 10000000 || terms.MinAge is null or < 18 or > 100 ||
            terms.MinLicenseYears is null or < 0 or > 80 || terms.Rates is null || terms.Rates.Count > 500 ||
            terms.ExtraOptionIds is null || terms.ExtraOptionIds.Length > 100 ||
            terms.ExtraOptionIds.Any(id => id == Guid.Empty) || terms.ExtraOptionIds.Distinct().Count() != terms.ExtraOptionIds.Length)
            throw new ArgumentException("Vehicle rental conditions are invalid.");
        foreach (var rate in terms.Rates)
            if (rate is null || rate.Id == Guid.Empty || rate.StartDate > rate.EndDate || rate.DailyPrice is <= 0 or > 1000000 ||
                rate.Multiplier is <= 0 or > 100 || rate.WeekdayMultiplier is <= 0 or > 100 ||
                rate.WeekendMultiplier is <= 0 or > 100 || rate.CalculationType is not ("fixed" or "multiplier"))
                throw new ArgumentException("Vehicle rate is invalid.");
        if (terms.Rates.Select(r => r.Id).Distinct().Count() != terms.Rates.Count)
            throw new ArgumentException("Vehicle rate identifiers must be unique.");
    }

    public static void ValidatePolicy(OfficeOperatingPolicy policy)
    {
        if (policy.MinimumNoticeMinutes is null or < 0 or > 525600 || policy.PreparationMinutes is null or < 0 or > 10080 ||
            policy.ClosedDates is null || policy.ClosedDates.Length > 730)
            throw new ArgumentException("Notice, preparation time and closed dates must be valid.");
        ValidateWindows(policy.PickupWindows);
        ValidateWindows(policy.ReturnWindows);
    }

    private static void ValidateWindows(List<OfficeOperatingWindow> windows)
    {
        if (windows is null || windows.Count is < 1 or > 56 || windows.Any(w =>
                w is null || !Enum.IsDefined(w.Day) || w.StartMinute < 0 || w.EndMinute > 1440 || w.StartMinute >= w.EndMinute))
            throw new ArgumentException("Configure valid pickup and return windows in Turkey local time.");
        foreach (var day in windows.GroupBy(w => w.Day))
        {
            var ordered = day.OrderBy(w => w.StartMinute).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (ordered[i].StartMinute < ordered[i - 1].EndMinute)
                    throw new ArgumentException("Operating windows cannot overlap.");
        }
    }

    public static void ValidateItinerary(Office? pickup, Office? dropoff, DateTime from, DateTime until, DateTime now)
    {
        if (pickup is not { IsActive: true, OperatingPolicy: not null } ||
            dropoff is not { IsActive: true, OperatingPolicy: not null })
            throw new ReservationQuoteConflictException("Office operating policies must be configured before booking.");
        ValidatePolicy(pickup.OperatingPolicy);
        ValidatePolicy(dropoff.OperatingPolicy);
        if (from <= now || from < now.AddMinutes(pickup.OperatingPolicy.MinimumNoticeMinutes!.Value) || from >= until ||
            !WithinWindow(pickup.OperatingPolicy, from, true) || !WithinWindow(dropoff.OperatingPolicy, until, false))
            throw new ReservationQuoteConflictException("Selected times do not meet office operating policies.");
    }

    private static bool WithinWindow(OfficeOperatingPolicy policy, DateTime utc, bool pickup)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(3);
        if (policy.ClosedDates.Contains(DateOnly.FromDateTime(local))) return false;
        var minute = local.TimeOfDay.TotalMinutes;
        return (pickup ? policy.PickupWindows : policy.ReturnWindows).Any(w =>
            w.Day == local.DayOfWeek && minute >= w.StartMinute && minute < w.EndMinute);
    }
}
