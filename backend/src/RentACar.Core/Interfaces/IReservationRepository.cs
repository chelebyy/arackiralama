using RentACar.Core.Entities;
using RentACar.Core.Enums;

namespace RentACar.Core.Interfaces;

public interface IReservationRepository : IRepository<Reservation>
{
    Task<Reservation?> GetByPublicCodeAsync(string publicCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> GetActiveReservationsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingReservationsAsync(
        Guid vehicleId,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        Guid? excludeReservationId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> GetReservationsByStatusAsync(
        ReservationStatus status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> GetExpiredReservationsAsync(
        DateTime beforeDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> SearchReservationsAsync(
        Guid? customerId = null,
        Guid? vehicleId = null,
        ReservationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
