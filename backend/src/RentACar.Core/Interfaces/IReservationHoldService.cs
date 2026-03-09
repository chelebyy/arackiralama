namespace RentACar.Core.Interfaces;

public interface IReservationHoldService
{
    Task<bool> CreateHoldAsync(
        Guid reservationId,
        Guid vehicleId,
        string sessionId,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task<bool> ExtendHoldAsync(
        Guid reservationId,
        TimeSpan extension,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseHoldAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<bool> IsHoldValidAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetExpiredHoldsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> IsVehicleHeldAsync(
        Guid vehicleId,
        string? excludeSessionId = null,
        CancellationToken cancellationToken = default);

    Task<ReservationHoldSnapshot?> GetHoldAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);
}

public sealed record ReservationHoldSnapshot(
    Guid ReservationId,
    Guid VehicleId,
    string SessionId,
    DateTime ExpiresAt);
