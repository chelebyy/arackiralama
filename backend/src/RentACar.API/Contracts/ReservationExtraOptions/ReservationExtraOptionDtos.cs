using RentACar.Core.Enums;

namespace RentACar.API.Contracts.ReservationExtraOptions;

public sealed record ReservationExtraOptionTranslationDto(
    string Locale,
    string Name,
    string Description);

public sealed record AdminReservationExtraOptionDto(
    Guid Id,
    string Code,
    decimal UnitPrice,
    ReservationExtraPricingMode PricingMode,
    int MaxQuantity,
    string IconKey,
    int SortOrder,
    bool IsActive,
    bool IsArchived,
    uint Version,
    DateTime UpdatedAt,
    IReadOnlyList<Guid> VehicleGroupIds,
    IReadOnlyList<ReservationExtraOptionTranslationDto> Translations);

public sealed record AdminReservationExtraOptionListResponse(
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<AdminReservationExtraOptionDto> Items);

public sealed record CreateReservationExtraOptionRequest(
    decimal UnitPrice,
    ReservationExtraPricingMode PricingMode,
    int MaxQuantity,
    string IconKey,
    int SortOrder,
    IReadOnlyList<Guid> VehicleGroupIds,
    IReadOnlyList<ReservationExtraOptionTranslationDto> Translations);

public sealed record UpdateReservationExtraOptionRequest(
    uint Version,
    decimal UnitPrice,
    ReservationExtraPricingMode PricingMode,
    int MaxQuantity,
    string IconKey,
    int SortOrder,
    IReadOnlyList<Guid> VehicleGroupIds,
    IReadOnlyList<ReservationExtraOptionTranslationDto> Translations);

public sealed record UpdateReservationExtraOptionStatusRequest(uint Version, bool IsActive);

public sealed record RestoreReservationExtraOptionRequest(uint Version);

public sealed record RestoreReservationExtraOptionResult(AdminReservationExtraOptionDto Item);

public sealed record DeleteReservationExtraOptionResult(string Disposition);

public sealed record PublicReservationExtraOptionDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    decimal UnitPrice,
    ReservationExtraPricingMode PricingMode,
    int MaxQuantity,
    string IconKey,
    int SortOrder,
    uint Version);

public sealed record PublicReservationExtraOptionCatalogResponse(
    IReadOnlyList<PublicReservationExtraOptionDto> Items);

public sealed record ReservationExtraOptionAuditContext(string? UserId, string? IpAddress);
