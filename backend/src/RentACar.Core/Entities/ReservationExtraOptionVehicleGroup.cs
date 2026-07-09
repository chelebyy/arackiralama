namespace RentACar.Core.Entities;

public class ReservationExtraOptionVehicleGroup
{
    public Guid OptionId { get; set; }
    public Guid VehicleGroupId { get; set; }

    public ReservationExtraOption? Option { get; set; }
    public VehicleGroup? VehicleGroup { get; set; }
}
