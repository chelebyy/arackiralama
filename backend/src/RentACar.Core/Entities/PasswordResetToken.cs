using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class PasswordResetToken : BaseEntity
{
    public AuthPrincipalType PrincipalType { get; set; }
    public Guid PrincipalId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    public bool IsConsumed => ConsumedAtUtc.HasValue;

    public bool IsActive(DateTime utcNow) => !IsConsumed && ExpiresAtUtc > utcNow;

    public bool TryConsume(DateTime consumedAtUtc)
    {
        if (!IsActive(consumedAtUtc))
        {
            return false;
        }

        ConsumedAtUtc = consumedAtUtc;
        return true;
    }
}
