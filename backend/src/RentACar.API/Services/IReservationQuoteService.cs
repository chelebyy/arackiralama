using RentACar.API.Contracts.Pricing;

namespace RentACar.API.Services;

public interface IReservationQuoteService
{
    Task<ReservationQuoteDto> CreateAsync(
        CreateReservationQuoteRequest request,
        string sessionId,
        CancellationToken cancellationToken = default);
}
