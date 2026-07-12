using RentACar.API.Contracts.Pricing;
using RentACar.Core.Entities;

namespace RentACar.API.Services;

public interface IReservationExtraPricingService
{
    Task<IReadOnlyList<ReservationQuotedExtraV1>> CalculateAsync(
        Guid vehicleGroupId,
        string locale,
        int rentalDays,
        IReadOnlyList<SelectedReservationExtraInput> selections,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationQuotedExtraV1>> CalculateLegacyAsync(
        Guid vehicleGroupId,
        string locale,
        int rentalDays,
        int extraDriverCount,
        int childSeatCount,
        CancellationToken cancellationToken = default);

    Task ValidateCurrentAvailabilityAsync(
        Guid vehicleGroupId,
        IReadOnlyList<ReservationQuotedExtraV1> quotedExtras,
        CancellationToken cancellationToken = default);
}
