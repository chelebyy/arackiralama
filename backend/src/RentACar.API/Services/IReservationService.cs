using RentACar.API.Contracts.Reservations;
using RentACar.Core.Enums;

namespace RentACar.API.Services;

public interface IReservationService
{
    // Availability Search
    Task<IReadOnlyList<AvailableVehicleGroupDto>> SearchAvailabilityAsync(
        Guid pickupOfficeId,
        Guid? returnOfficeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        Guid? vehicleGroupId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<bool> IsVehicleGroupAvailableAsync(
        Guid vehicleGroupId,
        Guid pickupOfficeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        CancellationToken cancellationToken = default);

    // Reservation CRUD
    Task<ReservationDto?> GetReservationByPublicCodeAsync(
        string publicCode,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> GetReservationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ReservationDto> CreateDraftReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> UpdateReservationAsync(
        Guid id,
        UpdateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> CancelReservationAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default);

    // Hold Management
    Task<ReservationHoldDto?> CreateHoldAsync(
        Guid reservationId,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<ReservationHoldDto?> ExtendHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseHoldByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    // Lifecycle Management
    Task<ReservationDto?> TransitionStatusAsync(
        Guid reservationId,
        ReservationStatus newStatus,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> ProcessPaymentAsync(
        Guid reservationId,
        PaymentInfoRequest paymentInfo,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> ConfirmPaymentAsync(
        Guid reservationId,
        string transactionId,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> CheckInAsync(
        Guid reservationId,
        CheckInRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> CheckOutAsync(
        Guid reservationId,
        CheckOutRequest request,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> ExpireReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task ProcessExpiredReservationsAsync(CancellationToken cancellationToken = default);

    // Admin Operations
    Task<IReadOnlyList<ReservationDto>> GetAllReservationsAsync(
        ReservationFilterRequest? filter = null,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> AssignVehicleAsync(
        Guid reservationId,
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> UnassignVehicleAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationDto?> AdminCancelReservationAsync(
        Guid reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    // Validation Helpers
    Task<bool> IsValidStatusTransitionAsync(
        ReservationStatus currentStatus,
        ReservationStatus newStatus,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationStatus>> GetValidNextStatusesAsync(
        ReservationStatus currentStatus,
        CancellationToken cancellationToken = default);

    Task<bool> CanHoldBeExtendedAsync(
        Guid holdId,
        CancellationToken cancellationToken = default);

    // Customer Reservations
    Task<IReadOnlyList<ReservationDto>> GetCustomerReservationsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
