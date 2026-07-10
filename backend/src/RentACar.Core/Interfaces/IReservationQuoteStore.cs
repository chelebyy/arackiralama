using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IReservationQuoteStore
{
    Task SaveAsync(ReservationQuoteV1 quote, CancellationToken cancellationToken = default);
    Task<ReservationQuoteV1?> GetAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<bool> TryClaimAsync(
        Guid quoteId,
        string claimOwner,
        TimeSpan claimDuration,
        CancellationToken cancellationToken = default);
    Task<bool> ReleaseClaimAsync(
        Guid quoteId,
        string claimOwner,
        CancellationToken cancellationToken = default);
    Task<bool> MarkConsumedAsync(
        Guid quoteId,
        string claimOwner,
        Guid reservationId,
        CancellationToken cancellationToken = default);
    Task<bool> ReconcileConsumedAsync(
        Guid quoteId,
        Guid reservationId,
        CancellationToken cancellationToken = default);
}
