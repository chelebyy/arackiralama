namespace RentACar.Core.Entities;

public sealed class GuestReservationAccess : BaseEntity
{
    public Guid ReservationId { get; set; }
    public string EmailHash { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CodeExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? SessionHash { get; set; }
    public string? CsrfHash { get; set; }
    public DateTime? SessionExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public string Locale { get; set; } = "tr";
}

public sealed class ReservationAmendment : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Guid AccessId { get; set; }
    public Guid QuoteId { get; set; }
    public uint ReservationVersion { get; set; }
    public string RequestJson { get; set; } = string.Empty;
    public string PolicyHash { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? PreviousSnapshot { get; set; }
    public string? ResultJson { get; set; }
}

