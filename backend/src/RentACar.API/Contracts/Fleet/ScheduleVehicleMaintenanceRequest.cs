namespace RentACar.API.Contracts.Fleet;

public sealed record ScheduleVehicleMaintenanceRequest(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? Notes);
