using RentACar.Core.Enums;

namespace RentACar.API.Contracts.Fleet;

public sealed record TransferVehicleRequest(
    Guid TargetOfficeId,
    VehicleStatus? Status);
