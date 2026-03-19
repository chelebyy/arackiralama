namespace RentACar.API.Contracts.Reservations;

public record ReservationDto
{
    public Guid Id { get; init; }
    public string PublicCode { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public Guid VehicleId { get; init; }
    public string? VehiclePlate { get; init; }
    public string VehicleBrand { get; init; } = string.Empty;
    public string VehicleModel { get; init; } = string.Empty;
    public Guid VehicleGroupId { get; init; }
    public string VehicleGroupName { get; init; } = string.Empty;
    public Guid PickupOfficeId { get; init; }
    public string PickupOfficeName { get; init; } = string.Empty;
    public Guid ReturnOfficeId { get; init; }
    public string ReturnOfficeName { get; init; } = string.Empty;
    public DateTime PickupDateTime { get; init; }
    public DateTime ReturnDateTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal DepositAmount { get; init; }
    public int RentalDays { get; init; }
    public string? CampaignCode { get; init; }
    public decimal DiscountAmount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CheckedInAt { get; init; }
    public DateTime? CheckedOutAt { get; init; }
    public string? Notes { get; init; }
    public ReservationHoldDto? ActiveHold { get; init; }
}

public record ReservationHoldDto
{
    public Guid Id { get; init; }
    public Guid ReservationId { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public int RemainingMinutes { get; init; }
    public bool IsExpired { get; init; }
}

public record AvailableVehicleGroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NameTr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameRu { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string NameDe { get; init; } = string.Empty;
    public decimal DailyPrice { get; init; }
    public decimal TotalPrice { get; init; }
    public decimal DepositAmount { get; init; }
    public int AvailableVehicleCount { get; init; }
    public int MinAge { get; init; }
    public int MinLicenseYears { get; init; }
    public List<string> Features { get; init; } = new();
    public string? PhotoUrl { get; init; }
    public int RentalDays { get; init; }
    public bool IsAvailable { get; init; }
    public List<PriceBreakdownItemDto> PriceBreakdown { get; init; } = new();
}

public record PriceBreakdownItemDto
{
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty; // base, fee, discount, extra
}

public record PaginatedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
}
