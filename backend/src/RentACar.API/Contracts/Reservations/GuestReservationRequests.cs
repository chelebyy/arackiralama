using System.ComponentModel.DataAnnotations;
using RentACar.Core.Entities;

namespace RentACar.API.Contracts.Reservations;

public sealed record GuestAccessRequest(
    [Required, StringLength(24)] string PublicCode,
    [Required, EmailAddress, StringLength(254)] string Email,
    [StringLength(5)] string Locale = "tr");

public sealed record GuestVerificationRequest(Guid ChallengeId, [Required, StringLength(64)] string Code);
public sealed record GuestCancellationRequest(uint Version, decimal AcceptedFee);
public sealed record GuestAmendmentRequest(DateTime PickupDateTimeUtc, DateTime ReturnDateTimeUtc,
    DriverDeclaration Declaration);
public sealed record GuestAmendmentConfirmation(Guid AmendmentId, decimal AcceptedTotal);
public sealed record GuestSessionResult(string SessionToken, string CsrfToken, DateTime ExpiresAt);

