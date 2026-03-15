using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class AuthSession : BaseEntity
{
    public AuthPrincipalType PrincipalType { get; set; }
    public Guid PrincipalId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsActive(DateTime utcNow) => !IsRevoked && RefreshTokenExpiresAtUtc > utcNow;
}
