namespace RentACar.API.Contracts.Reservations;

public record CreateReservationRequest
{
    public Guid VehicleGroupId { get; init; }
    public Guid PickupOfficeId { get; init; }
    public Guid ReturnOfficeId { get; init; }
    public DateTime PickupDateTimeUtc { get; init; }
    public DateTime ReturnDateTimeUtc { get; init; }
    public CustomerInfoRequest Customer { get; init; } = new();
    public string? CampaignCode { get; init; }
    public int ExtraDriverCount { get; init; }
    public int ChildSeatCount { get; init; }
    public int? DriverAge { get; init; }
    public bool FullCoverageWaiver { get; init; }
    public string? Notes { get; init; }
    public string? SessionId { get; init; }
}

public record CustomerInfoRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? IdentityNumber { get; init; }
    public string? DriverLicenseNumber { get; init; }
    public DateTime? DriverLicenseIssueDate { get; init; }
    public DateTime? DateOfBirth { get; init; }
}

public record UpdateReservationRequest
{
    public DateTime? PickupDateTimeUtc { get; init; }
    public DateTime? ReturnDateTimeUtc { get; init; }
    public Guid? PickupOfficeId { get; init; }
    public Guid? ReturnOfficeId { get; init; }
    public CustomerInfoRequest? Customer { get; init; }
    public string? Notes { get; init; }
}

public record PaymentInfoRequest
{
    public string PaymentMethod { get; init; } = string.Empty; // credit_card, bank_transfer
    public string? CardToken { get; init; }
    public bool SaveCardForFuture { get; init; }
}

public record CheckInRequest
{
    public string? Notes { get; init; }
    public int? ActualFuelLevel { get; init; }
    public int? ActualMileage { get; init; }
    public List<string>? VehicleConditionPhotos { get; init; }
}

public record CheckOutRequest
{
    public string? Notes { get; init; }
    public int? ReturnFuelLevel { get; init; }
    public int? ReturnMileage { get; init; }
    public List<string>? VehicleConditionPhotos { get; init; }
    public bool IsDamaged { get; init; }
    public decimal? DamageFee { get; init; }
}

public record ReservationFilterRequest
{
    public Guid? CustomerId { get; init; }
    public Guid? VehicleId { get; init; }
    public string? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? SearchTerm { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
