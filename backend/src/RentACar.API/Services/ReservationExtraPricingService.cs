using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Pricing;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class ReservationExtraPricingService(IApplicationDbContext dbContext)
    : IReservationExtraPricingService
{
    private static readonly HashSet<string> SupportedLocales =
        new(["tr", "en", "de", "ru", "ar"], StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ReservationQuotedExtraV1>> CalculateAsync(
        Guid vehicleGroupId,
        string locale,
        int rentalDays,
        IReadOnlyList<SelectedReservationExtraInput> selections,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocale = NormalizeLocale(locale);
        if (rentalDays < 1)
        {
            throw new ArgumentException("Rental duration must be at least one day.");
        }

        if (selections.Count == 0)
        {
            return [];
        }

        if (selections.Any(selection => selection.OptionId == Guid.Empty || selection.Quantity < 1))
        {
            throw new ArgumentException("Extra option identifiers and quantities must be valid.");
        }

        var optionIds = selections.Select(selection => selection.OptionId).ToArray();
        if (optionIds.Distinct().Count() != optionIds.Length)
        {
            throw new ArgumentException("Duplicate reservation extra options are not allowed.");
        }

        var options = await dbContext.ReservationExtraOptions
            .AsNoTracking()
            .Where(option => optionIds.Contains(option.Id))
            .Include(option => option.Translations.Where(translation => translation.Locale == normalizedLocale))
            .Include(option => option.VehicleGroups.Where(group => group.VehicleGroupId == vehicleGroupId))
            .ToDictionaryAsync(option => option.Id, cancellationToken);

        if (options.Count != optionIds.Length)
        {
            throw new ReservationQuoteConflictException("One or more reservation extra options no longer exist.");
        }

        var result = new List<ReservationQuotedExtraV1>(selections.Count);
        foreach (var selection in selections)
        {
            var option = options[selection.OptionId];
            ValidateQuoteSelection(option, selection, vehicleGroupId);
            var translation = option.Translations.SingleOrDefault()
                ?? throw new ArgumentException($"Extra option {option.Code} has no {normalizedLocale} translation.");
            var total = option.PricingMode == ReservationExtraPricingMode.PerDay
                ? Round(option.UnitPrice * rentalDays * selection.Quantity)
                : Round(option.UnitPrice * selection.Quantity);

            result.Add(new ReservationQuotedExtraV1
            {
                ExtraOptionId = option.Id,
                OptionVersion = option.Version,
                Code = option.Code,
                Locale = normalizedLocale,
                Name = translation.Name,
                Description = translation.Description,
                UnitPrice = option.UnitPrice,
                PricingMode = ToWireValue(option.PricingMode),
                Quantity = selection.Quantity,
                RentalDays = rentalDays,
                Total = total
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ReservationQuotedExtraV1>> CalculateLegacyAsync(
        Guid vehicleGroupId,
        string locale,
        int rentalDays,
        int extraDriverCount,
        int childSeatCount,
        CancellationToken cancellationToken = default)
    {
        if (extraDriverCount < 0 || childSeatCount < 0)
        {
            throw new ArgumentException("Legacy extra quantities cannot be negative.");
        }

        var requestedCodes = new List<(string Code, int Quantity)>();
        if (extraDriverCount > 0)
        {
            requestedCodes.Add(("additional_driver", extraDriverCount));
        }
        if (childSeatCount > 0)
        {
            requestedCodes.Add(("child_seat", childSeatCount));
        }
        if (requestedCodes.Count == 0)
        {
            return [];
        }

        var codes = requestedCodes.Select(item => item.Code).ToArray();
        var options = await dbContext.ReservationExtraOptions
            .AsNoTracking()
            .Where(option => codes.Contains(option.Code))
            .ToDictionaryAsync(option => option.Code, StringComparer.Ordinal, cancellationToken);

        if (options.Count != codes.Length)
        {
            throw new InvalidOperationException("A legacy reservation extra option is not configured.");
        }

        var selections = requestedCodes.Select(item => new SelectedReservationExtraInput
        {
            OptionId = options[item.Code].Id,
            OptionVersion = options[item.Code].Version,
            Quantity = item.Quantity
        }).ToArray();

        return await CalculateAsync(vehicleGroupId, locale, rentalDays, selections, cancellationToken);
    }

    public async Task ValidateCurrentAvailabilityAsync(
        Guid vehicleGroupId,
        IReadOnlyList<ReservationQuotedExtraV1> quotedExtras,
        CancellationToken cancellationToken = default)
    {
        if (quotedExtras.Count == 0)
        {
            return;
        }

        var optionIds = quotedExtras.Select(item => item.ExtraOptionId).ToArray();
        var options = await dbContext.ReservationExtraOptions
            .AsNoTracking()
            .Where(option => optionIds.Contains(option.Id))
            .Include(option => option.VehicleGroups.Where(group => group.VehicleGroupId == vehicleGroupId))
            .ToDictionaryAsync(option => option.Id, cancellationToken);

        if (options.Count != optionIds.Length)
        {
            throw new ReservationQuoteConflictException("A quoted extra option is no longer available.");
        }

        foreach (var quotedExtra in quotedExtras)
        {
            var option = options[quotedExtra.ExtraOptionId];
            var currentMode = ToWireValue(option.PricingMode);
            if (!option.IsActive || option.IsArchived ||
                option.VehicleGroups.Count == 0 ||
                option.MaxQuantity < quotedExtra.Quantity ||
                !string.Equals(currentMode, quotedExtra.PricingMode, StringComparison.Ordinal))
            {
                throw new ReservationQuoteConflictException("A quoted extra option is no longer available for this reservation.");
            }
        }
    }

    private static void ValidateQuoteSelection(
        ReservationExtraOption option,
        SelectedReservationExtraInput selection,
        Guid vehicleGroupId)
    {
        if (!option.IsActive || option.IsArchived)
        {
            throw new ReservationQuoteConflictException($"Extra option {option.Code} is not active.");
        }
        if (option.Version != selection.OptionVersion)
        {
            throw new ReservationQuoteConflictException($"Extra option {option.Code} is stale.");
        }
        if (selection.Quantity > option.MaxQuantity)
        {
            throw new ReservationQuoteConflictException($"Extra option {option.Code} exceeds its maximum quantity.");
        }
        if (option.VehicleGroups.All(group => group.VehicleGroupId != vehicleGroupId))
        {
            throw new ReservationQuoteConflictException($"Extra option {option.Code} is not available for this vehicle group.");
        }
    }

    private static string NormalizeLocale(string locale)
    {
        var normalized = string.IsNullOrWhiteSpace(locale) ? "tr" : locale.Trim().ToLowerInvariant();
        return SupportedLocales.Contains(normalized)
            ? normalized
            : throw new ArgumentException("Unsupported reservation locale.");
    }

    private static string ToWireValue(ReservationExtraPricingMode mode) =>
        mode == ReservationExtraPricingMode.PerDay ? "PER_DAY" : "PER_RENTAL";

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public sealed class ReservationQuoteConflictException(string message) : InvalidOperationException(message);
