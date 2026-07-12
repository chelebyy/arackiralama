namespace RentACar.Core.Entities;

public sealed class ReservationQuoteReplayProofV1
{
    public int SchemaVersion { get; set; } = 1;
    public string SessionHash { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
