using System.Text.Json.Serialization;

namespace RentACar.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<ReservationExtraPricingMode>))]
public enum ReservationExtraPricingMode
{
    [JsonStringEnumMemberName("PER_DAY")]
    PerDay,
    [JsonStringEnumMemberName("PER_RENTAL")]
    PerRental
}
