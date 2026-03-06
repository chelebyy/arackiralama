using RentACar.Core.Enums;

namespace RentACar.API.Contracts.Fleet;

public sealed record UpdateVehicleStatusRequest(
    VehicleStatus Status);
