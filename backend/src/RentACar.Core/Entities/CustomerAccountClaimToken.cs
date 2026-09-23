namespace RentACar.Core.Entities;

public class CustomerAccountClaimToken : BaseEntity
{
    public Guid CustomerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public string? IssuedFromIp { get; set; }
    public string? IssuedUserAgent { get; set; }

    public bool IsConsumed => ConsumedAtUtc.HasValue;
    public bool IsSuperseded => SupersededAtUtc.HasValue;

    public bool IsActive(DateTime utcNow) =>
        !IsConsumed && !IsSuperseded && ExpiresAtUtc > utcNow;

    public bool TryConsume(DateTime consumedAtUtc)
    {
        if (!IsActive(consumedAtUtc))
        {
            return false;
        }

        ConsumedAtUtc = consumedAtUtc;
        return true;
    }

    public void Supersede(DateTime supersededAtUtc)
    {
        if (!IsConsumed && !IsSuperseded)
        {
            SupersededAtUtc = supersededAtUtc;
        }
    }
}