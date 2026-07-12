using RentACar.API.Contracts.ReservationExtraOptions;

namespace RentACar.API.Services;

public interface IReservationExtraOptionCatalogService
{
    Task<AdminReservationExtraOptionListResponse> GetAdminListAsync(
        string? search,
        string? status,
        Guid? vehicleGroupId,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PublicReservationExtraOptionCatalogResponse> GetPublicCatalogAsync(
        Guid vehicleGroupId,
        string locale,
        CancellationToken cancellationToken = default);

    Task<AdminReservationExtraOptionDto> CreateAsync(
        CreateReservationExtraOptionRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default);

    Task<AdminReservationExtraOptionDto> UpdateAsync(
        Guid id,
        UpdateReservationExtraOptionRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default);

    Task<AdminReservationExtraOptionDto> UpdateStatusAsync(
        Guid id,
        UpdateReservationExtraOptionStatusRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default);

    Task<RestoreReservationExtraOptionResult> RestoreAsync(
        Guid id,
        RestoreReservationExtraOptionRequest request,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default);

    Task<DeleteReservationExtraOptionResult> DeleteAsync(
        Guid id,
        uint version,
        ReservationExtraOptionAuditContext auditContext,
        CancellationToken cancellationToken = default);
}

public sealed class ReservationExtraOptionNotFoundException : Exception
{
    public ReservationExtraOptionNotFoundException() : base("Reservation extra option was not found.")
    {
    }
}

public sealed class ReservationExtraOptionConcurrencyException : Exception
{
    public ReservationExtraOptionConcurrencyException() : base("Reservation extra option was updated by another session. Reload before saving.")
    {
    }
}
