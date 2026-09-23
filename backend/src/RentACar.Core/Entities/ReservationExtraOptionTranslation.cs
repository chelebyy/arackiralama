namespace RentACar.Core.Entities;

public class ReservationExtraOptionTranslation
{
    public Guid OptionId { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ReservationExtraOption? Option { get; set; }
}
