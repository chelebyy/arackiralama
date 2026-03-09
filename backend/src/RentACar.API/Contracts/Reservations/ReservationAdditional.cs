namespace RentACar.API.Contracts.Reservations;

public record SearchAvailabilityRequest
{
    public Guid PickupOfficeId { get; init; }
    public Guid? ReturnOfficeId { get; init; }
    public DateTime PickupDateTimeUtc { get; init; }
    public DateTime ReturnDateTimeUtc { get; init; }
    public Guid? VehicleGroupId { get; init; }
}

public record CreateHoldRequest
{
    public Guid ReservationId { get; init; }
    public string SessionId { get; init; } = string.Empty;
}

public record ExtendHoldRequest
{
    public Guid HoldId { get; init; }
}

public record CancelReservationRequest
{
    public string? Reason { get; init; }
}

public record AssignVehicleRequest
{
    public Guid VehicleId { get; init; }
}

public record StatusTransitionRequest
{
    public string NewStatus { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public record ReservationSummaryDto
{
    public Guid Id { get; init; }
    public string PublicCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string VehicleGroupName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime PickupDateTime { get; init; }
    public DateTime ReturnDateTime { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record PaginatedReservationsResponse
{
    public IReadOnlyList<ReservationSummaryDto> Items { get; init; } = new List<ReservationSummaryDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
}
