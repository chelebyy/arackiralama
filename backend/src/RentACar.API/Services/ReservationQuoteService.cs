using RentACar.API.Contracts.Pricing;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class ReservationQuoteService(
    IPricingService pricingService,
    IReservationExtraPricingService extraPricingService,
    IReservationQuoteStore quoteStore,
    ILogger<ReservationQuoteService> logger) : IReservationQuoteService
{
    private static readonly TimeSpan QuoteLifetime = TimeSpan.FromMinutes(15);

    public async Task<ReservationQuoteDto> CreateAsync(
        CreateReservationQuoteRequest request,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, sessionId);

        var returnOfficeId = request.ReturnOfficeId == Guid.Empty
            ? request.PickupOfficeId
            : request.ReturnOfficeId;
        var campaignCode = NormalizeCampaignCode(request.CampaignCode);
        if (!await pricingService.VehicleGroupExistsAsync(request.VehicleGroupId, cancellationToken))
        {
            throw new ArgumentException("Vehicle group does not exist.");
        }
        if (!await pricingService.OfficeExistsAsync(request.PickupOfficeId, cancellationToken) ||
            !await pricingService.OfficeExistsAsync(returnOfficeId, cancellationToken))
        {
            throw new ArgumentException("Pickup or return office does not exist.");
        }
        var rentalDays = CalculateRentalDays(request.PickupDateTimeUtc, request.ReturnDateTimeUtc);
        if (campaignCode is not null && !await pricingService.IsCampaignCodeValidAsync(
                campaignCode,
                request.VehicleGroupId,
                rentalDays,
                DateOnly.FromDateTime(request.PickupDateTimeUtc),
                cancellationToken))
        {
            throw new ArgumentException("Campaign code is invalid or expired.");
        }
        var baseBreakdown = await pricingService.CalculateBreakdownAsync(
            request.VehicleGroupId,
            request.PickupOfficeId,
            returnOfficeId,
            request.PickupDateTimeUtc,
            request.ReturnDateTimeUtc,
            campaignCode,
            0,
            0,
            request.DriverAge,
            request.FullCoverageWaiver,
            cancellationToken)
            ?? throw new ArgumentException("No pricing rule exists for the selected dates.");

        var quotedExtras = await extraPricingService.CalculateAsync(
            request.VehicleGroupId,
            request.Locale,
            baseBreakdown.RentalDays,
            request.SelectedExtras,
            cancellationToken);
        var extraTotal = Round(quotedExtras.Sum(item => item.Total));
        var finalTotal = Round(baseBreakdown.FinalTotal + extraTotal);
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.Add(QuoteLifetime);
        var quoteId = Guid.NewGuid();
        var snapshot = BuildSnapshot(
            quoteId,
            issuedAtUtc,
            expiresAtUtc,
            baseBreakdown,
            quotedExtras,
            extraTotal,
            finalTotal);

        var quote = new ReservationQuoteV1
        {
            QuoteId = quoteId,
            SessionHash = ReservationQuoteSecurity.HashSessionId(sessionId),
            VehicleGroupId = request.VehicleGroupId,
            PickupOfficeId = request.PickupOfficeId,
            ReturnOfficeId = returnOfficeId,
            PickupDateTimeUtc = NormalizeUtc(request.PickupDateTimeUtc),
            ReturnDateTimeUtc = NormalizeUtc(request.ReturnDateTimeUtc),
            CampaignCode = campaignCode,
            DriverAge = request.DriverAge,
            FullCoverageWaiver = request.FullCoverageWaiver,
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "tr" : request.Locale.Trim().ToLowerInvariant(),
            SelectedExtras = quotedExtras.ToList(),
            PricingSnapshot = snapshot,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
        await quoteStore.SaveAsync(quote, cancellationToken);

        logger.LogInformation(
            "Reservation quote {QuoteId} created for vehicle group {VehicleGroupId} with {ExtraCount} extras",
            quoteId,
            request.VehicleGroupId,
            quotedExtras.Count);

        var responseBreakdown = baseBreakdown with
        {
            ExtrasTotal = extraTotal,
            FinalTotal = finalTotal,
            ExtraItems = quotedExtras.Select(ToDto).ToArray()
        };
        return new ReservationQuoteDto(
            quoteId,
            expiresAtUtc,
            responseBreakdown.DailyRate,
            responseBreakdown.RentalDays,
            responseBreakdown.BaseTotal,
            responseBreakdown.ExtrasTotal,
            responseBreakdown.CampaignDiscount,
            responseBreakdown.AirportFee,
            responseBreakdown.OneWayFee,
            responseBreakdown.ExtraDriverFee,
            responseBreakdown.ChildSeatFee,
            responseBreakdown.YoungDriverFee,
            responseBreakdown.FullCoverageWaiverFee,
            responseBreakdown.FinalTotal,
            responseBreakdown.DepositAmount,
            responseBreakdown.PreAuthorizationAmount,
            responseBreakdown.Currency,
            responseBreakdown.AppliedCampaignCode,
            responseBreakdown.ExtraItems);
    }

    private static ReservationPricingSnapshotV1 BuildSnapshot(
        Guid quoteId,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        PriceBreakdownDto baseBreakdown,
        IReadOnlyList<ReservationQuotedExtraV1> quotedExtras,
        decimal extraTotal,
        decimal finalTotal) => new()
        {
            SchemaVersion = 1,
            DailyRate = baseBreakdown.DailyRate,
            RentalDays = baseBreakdown.RentalDays,
            BaseTotal = baseBreakdown.BaseTotal,
            AirportFee = baseBreakdown.AirportFee,
            OneWayFee = baseBreakdown.OneWayFee,
            YoungDriverFee = baseBreakdown.YoungDriverFee,
            CoverageWaiverFee = baseBreakdown.FullCoverageWaiverFee,
            OtherFees = baseBreakdown.ExtraDriverFee + baseBreakdown.ChildSeatFee,
            CampaignId = baseBreakdown.AppliedCampaignId,
            CampaignCode = baseBreakdown.AppliedCampaignCode,
            DiscountType = baseBreakdown.AppliedCampaignDiscountType,
            DiscountValue = baseBreakdown.AppliedCampaignDiscountValue,
            DiscountTotal = baseBreakdown.CampaignDiscount,
            ExtraItems = quotedExtras.Select(item => new ReservationPricingExtraSnapshot
            {
                ExtraOptionId = item.ExtraOptionId,
                OptionVersion = item.OptionVersion,
                Code = item.Code,
                Name = item.Name,
                UnitPrice = item.UnitPrice,
                PricingMode = item.PricingMode,
                Quantity = item.Quantity,
                RentalDays = item.RentalDays,
                Total = item.Total
            }).ToList(),
            ExtrasTotal = extraTotal,
            DepositAmount = baseBreakdown.DepositAmount,
            PreAuthorizationAmount = baseBreakdown.PreAuthorizationAmount,
            Currency = baseBreakdown.Currency,
            FinalTotal = finalTotal,
            QuoteId = quoteId,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };

    private static ReservationExtraLineItemDto ToDto(ReservationQuotedExtraV1 item) => new(
        item.ExtraOptionId,
        item.OptionVersion,
        item.Code,
        item.Name,
        item.Description,
        item.UnitPrice,
        item.PricingMode,
        item.Quantity,
        item.RentalDays,
        item.Total);

    private static void ValidateRequest(CreateReservationQuoteRequest request, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("X-Session-Id header is required.");
        }
        if (request.VehicleGroupId == Guid.Empty || request.PickupOfficeId == Guid.Empty)
        {
            throw new ArgumentException("Vehicle group and pickup office are required.");
        }
        if (request.PickupDateTimeUtc >= request.ReturnDateTimeUtc)
        {
            throw new ArgumentException("Pickup date must be before return date.");
        }
        if (request.DriverAge is < 18)
        {
            throw new ArgumentException("Driver age must be at least 18.");
        }
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? NormalizeCampaignCode(string? campaignCode) =>
        string.IsNullOrWhiteSpace(campaignCode) ? null : campaignCode.Trim().ToUpperInvariant();

    private static int CalculateRentalDays(DateTime pickupDateTimeUtc, DateTime returnDateTimeUtc) =>
        Math.Max(1, DateOnly.FromDateTime(returnDateTimeUtc).DayNumber - DateOnly.FromDateTime(pickupDateTimeUtc).DayNumber);

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
