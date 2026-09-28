using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Data;

namespace RentACar.API.Services;

public sealed class GuestReservationService(
    RentACarDbContext db, INotificationQueueService notifications, IDataProtectionProvider protection,
    ReservationQuoteService quotes, IReservationQuoteStore quoteStore, VehicleBookingService booking,
    AvailabilityCacheInvalidationSignal invalidation)
{
    public const string MailPurpose = "GuestReservationEmail.v1";
    public const string AccessTemplate = "guest-reservation-access";
    public const string ChangedTemplate = "guest-reservation-changed";
    public const string CancelledTemplate = "guest-reservation-cancelled";

    public async Task<Guid> RequestAccessAsync(GuestAccessRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var normalized = Customer.NormalizeEmail(request.Email);
        var reservation = await db.Reservations.Include(r => r.Customer)
            .SingleOrDefaultAsync(r => r.PublicCode == request.PublicCode.Trim().ToUpper(), ct);
        if (reservation?.Customer?.NormalizedEmail != normalized) return id;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockReservationAsync(reservation.Id, ct);
        if (await db.GuestReservationAccess.AnyAsync(g => g.ReservationId == reservation.Id &&
                g.CreatedAt > DateTime.UtcNow.AddMinutes(-1), ct)) return id;
        await db.GuestReservationAccess.Where(g => g.ReservationId == reservation.Id &&
            !g.Revoked && g.VerifiedAt == null).ExecuteUpdateAsync(update => update
                .SetProperty(g => g.Revoked, true).SetProperty(g => g.CodeHash, string.Empty), ct);
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var grant = new GuestReservationAccess
        {
            Id = id, ReservationId = reservation.Id, EmailHash = Hash(normalized),
            CodeHash = Hash(id.ToString("N") + code), CodeExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Locale = Locale(request.Locale)
        };
        db.GuestReservationAccess.Add(grant);
        await notifications.EnqueueEmailAsync(new QueuedEmailNotificationRequest
        {
            ToEmail = reservation.Customer.Email, Locale = grant.Locale, TemplateKey = AccessTemplate,
            Variables = new Dictionary<string, string>
            {
                ["ChallengeId"] = id.ToString("D"),
                ["ProtectedCode"] = protection.CreateProtector(MailPurpose).Protect(code),
                ["ExpiresAtUtc"] = grant.CodeExpiresAt.ToString("O")
            }
        }, cancellationToken: ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<GuestSessionResult?> VerifyAsync(GuestVerificationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM guest_reservation_access WHERE \"Id\" = {request.ChallengeId} FOR UPDATE", ct);
        var grant = await db.GuestReservationAccess.SingleOrDefaultAsync(g => g.Id == request.ChallengeId, ct);
        if (grant is null || grant.Revoked || grant.VerifiedAt.HasValue || grant.CodeExpiresAt <= DateTime.UtcNow ||
            grant.Attempts >= 5) return null;
        grant.Attempts++;
        var submitted = Hash(grant.Id.ToString("N") + request.Code.Trim().ToUpperInvariant());
        if (!EqualHash(grant.CodeHash, submitted))
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return null;
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var csrf = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        grant.VerifiedAt = DateTime.UtcNow;
        grant.CodeHash = string.Empty;
        grant.SessionHash = Hash(token);
        grant.CsrfHash = Hash(csrf);
        grant.SessionExpiresAt = DateTime.UtcNow.AddMinutes(20);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new GuestSessionResult(token, csrf, grant.SessionExpiresAt.Value);
    }

    public async Task<GuestReservationAccess?> AuthenticateAsync(string? token, string? csrf, bool mutation, CancellationToken ct)
    {
        if (token is null || token.Length != 64 || (mutation && (csrf is null || csrf.Length != 64))) return null;
        var hash = Hash(token);
        var grant = await db.GuestReservationAccess.SingleOrDefaultAsync(g => g.SessionHash == hash &&
            !g.Revoked && g.SessionExpiresAt > DateTime.UtcNow && g.VerifiedAt != null, ct);
        if (grant is null || (mutation && !EqualHash(grant.CsrfHash!, Hash(csrf!)))) return null;
        var email = await db.Reservations.Where(r => r.Id == grant.ReservationId)
            .Select(r => r.Customer!.NormalizedEmail).SingleOrDefaultAsync(ct);
        return email is not null && EqualHash(grant.EmailHash, Hash(email)) ? grant : null;
    }

    public async Task<object> ViewAsync(GuestReservationAccess access, CancellationToken ct)
    {
        var r = await LoadAsync(access.ReservationId, ct);
        var policy = r.PickupOffice?.OperatingPolicy?.GuestManagement;
        return new
        {
            r.PublicCode, Status = r.Status.ToString(), r.Version, r.PickupDateTime, r.ReturnDateTime,
            Vehicle = r.Vehicle is null ? null : r.Vehicle.Brand + " " + r.Vehicle.Model,
            PickupOffice = r.PickupOffice?.Name, ReturnOffice = r.ReturnOffice?.Name,
            r.TotalAmount, Currency = r.PricingSnapshot?.Currency ?? "TRY",
            CanCancel = CanManage(r, policy, false), CanChangeDates = CanManage(r, policy, true),
            policy?.CancellationFee, policy?.ChangeFee, policy?.CancellationNoticeMinutes, policy?.ChangeNoticeMinutes
        };
    }

    public async Task LogoutAsync(GuestReservationAccess access, CancellationToken ct)
    {
        access.Revoked = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<object> CancelAsync(GuestReservationAccess access, GuestCancellationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockReservationAsync(access.ReservationId, ct);
        var r = await LoadAsync(access.ReservationId, ct);
        if (r.Status == ReservationStatus.Cancelled) return new { Status = "Cancelled" };
        await LockPolicyAsync(r, ct);
        var policy = r.PickupOffice?.OperatingPolicy?.GuestManagement;
        if (!CanManage(r, policy, false) || r.Version != request.Version || policy!.CancellationFee != request.AcceptedFee)
            throw new ReservationQuoteConflictException("Cancellation conditions changed or cancellation is unavailable.");
        await UpdateRemindersAsync(r, access.Locale, true, ct);
        r.Status = ReservationStatus.Cancelled;
        r.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { Action = "GuestReservationCancelled", EntityType = "Reservation",
            EntityId = r.Id.ToString(), Details = JsonSerializer.Serialize(new { Fee = request.AcceptedFee, Currency = r.PricingSnapshot!.Currency }) });
        await notifications.EnqueueEmailAsync(Message(r, access.Locale, CancelledTemplate,
            new Dictionary<string, string> { ["Fee"] = request.AcceptedFee.ToString("0.00", CultureInfo.InvariantCulture) }), cancellationToken: ct);
        await tx.CommitAsync(ct);
        invalidation.Invalidate();
        return new { Status = "Cancelled" };
    }

    public async Task<object> QuoteAmendmentAsync(GuestReservationAccess access, GuestAmendmentRequest request, CancellationToken ct)
    {
        var r = await LoadAsync(access.ReservationId, ct);
        var policy = r.PickupOffice?.OperatingPolicy?.GuestManagement;
        if (!CanManage(r, policy, true)) throw new ReservationQuoteConflictException("Date changes are unavailable.");
        var optionIds = r.SelectedExtras.Select(e => e.ExtraOptionId).ToArray();
        var currentOptions = await db.ReservationExtraOptions.AsNoTracking().Where(e => optionIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);
        if (currentOptions.Count != optionIds.Length) throw new ReservationQuoteConflictException("Selected extra is unavailable.");
        var previousFees = await PreviousAmendmentFeesAsync(r.Id, ct);
        var input = new CreateReservationQuoteRequest
        {
            VehicleId = r.VehicleId, VehicleGroupId = r.Vehicle?.GroupId ?? Guid.Empty,
            PickupOfficeId = r.PickupOfficeId, ReturnOfficeId = r.ReturnOfficeId,
            PickupDateTimeUtc = request.PickupDateTimeUtc, ReturnDateTimeUtc = request.ReturnDateTimeUtc,
            DriverAge = request.Declaration.AgeAtPickup, Locale = access.Locale,
            CampaignCode = r.PricingSnapshot!.CampaignCode,
            FullCoverageWaiver = r.PricingSnapshot.CoverageWaiverFee > 0,
            SelectedExtras = r.SelectedExtras.Select(e => new SelectedReservationExtraInput
                { OptionId = e.ExtraOptionId, OptionVersion = currentOptions[e.ExtraOptionId].Version, Quantity = e.Quantity }).ToArray()
        };
        var offer = await quotes.CreateAmendmentAsync(input, access.Id.ToString("N"), r.Id, ct);
        VehicleBookingService.ValidateDeclaration(request.Declaration, input.DriverAge,
            new ReservationBookingConditions { MinAge = offer.Conditions!.MinAge, MinLicenseYears = offer.Conditions.MinLicenseYears });
        var amendment = new ReservationAmendment
        {
            ReservationId = r.Id, AccessId = access.Id, QuoteId = offer.QuoteId, ReservationVersion = r.Version,
            RequestJson = JsonSerializer.Serialize(request), PolicyHash = Hash(JsonSerializer.Serialize(policy)),
            Fee = policy!.ChangeFee!.Value, ExpiresAt = offer.ExpiresAtUtc
        };
        db.ReservationAmendments.Add(amendment);
        await db.SaveChangesAsync(ct);
        return new { AmendmentId = amendment.Id, offer.ExpiresAtUtc, RentalTotal = offer.FinalTotal,
            Fee = amendment.Fee, PreviousFees = previousFees, FinalTotal = offer.FinalTotal + previousFees + amendment.Fee,
            Difference = offer.FinalTotal + previousFees + amendment.Fee - r.TotalAmount, offer.Currency, offer.Conditions };
    }

    public async Task<object> ConfirmAmendmentAsync(GuestReservationAccess access, GuestAmendmentConfirmation request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockReservationAsync(access.ReservationId, ct);
        var amendment = await db.ReservationAmendments.SingleOrDefaultAsync(a => a.Id == request.AmendmentId &&
            a.AccessId == access.Id && a.ReservationId == access.ReservationId, ct)
            ?? throw new ReservationQuoteConflictException("Amendment is unavailable.");
        if (amendment.AppliedAt.HasValue)
        {
            var result = JsonSerializer.Deserialize<AmendmentResult>(amendment.ResultJson!)!;
            if (result.TotalAmount != request.AcceptedTotal) throw new ReservationQuoteConflictException("Accepted amount changed.");
            return result;
        }
        var r = await LoadAsync(access.ReservationId, ct);
        await LockPolicyAsync(r, ct);
        var policy = r.PickupOffice?.OperatingPolicy?.GuestManagement;
        if (!CanManage(r, policy, true) || r.Version != amendment.ReservationVersion ||
            amendment.ExpiresAt <= DateTime.UtcNow || Hash(JsonSerializer.Serialize(policy)) != amendment.PolicyHash)
            throw new ReservationQuoteConflictException("Amendment conditions changed. Request a new quote.");
        var saved = JsonSerializer.Deserialize<GuestAmendmentRequest>(amendment.RequestJson)!;
        var quote = await quoteStore.GetAsync(amendment.QuoteId, ct)
            ?? throw new ReservationQuoteConflictException("Amendment quote expired.");
        var previousFees = await PreviousAmendmentFeesAsync(r.Id, ct);
        if (quote.AmendmentReservationId != r.Id || !ReservationQuoteSecurity.SessionHashMatches(quote.SessionHash, access.Id.ToString("N")) ||
            quote.VehicleId != r.VehicleId || quote.ExpiresAtUtc <= DateTime.UtcNow ||
            quote.PricingSnapshot.FinalTotal + previousFees + amendment.Fee != request.AcceptedTotal)
            throw new ReservationQuoteConflictException("Amendment amount changed.");
        await booking.ValidateQuoteAsync(quote, new CreateReservationRequest
        {
            Customer = new(), Driver = new DriverInfoRequest { Declaration = saved.Declaration }
        }, ct, r.Id);
        amendment.PreviousSnapshot = JsonSerializer.Serialize(new
        {
            r.PickupDateTime, r.ReturnDateTime, r.OccupiedUntilUtc, r.TotalAmount, r.PricingSnapshot
        });
        r.PickupDateTime = quote.PickupDateTimeUtc;
        r.ReturnDateTime = quote.ReturnDateTimeUtc;
        r.OccupiedUntilUtc = quote.ReturnDateTimeUtc.AddMinutes(quote.PricingSnapshot.BookingConditions!.PreparationMinutes);
        r.PricingSnapshot = quote.PricingSnapshot;
        r.PricingSnapshot.OtherFees += previousFees + amendment.Fee;
        r.PricingSnapshot.FinalTotal += previousFees + amendment.Fee;
        r.TotalAmount = request.AcceptedTotal;
        r.UpdatedAt = DateTime.UtcNow;
        foreach (var selected in r.SelectedExtras)
        {
            var replacement = quote.SelectedExtras.Single(e => e.ExtraOptionId == selected.ExtraOptionId);
            selected.OptionVersionSnapshot = replacement.OptionVersion;
            selected.OptionCodeSnapshot = replacement.Code;
            selected.NameSnapshot = replacement.Name;
            selected.DescriptionSnapshot = replacement.Description;
            selected.PricingModeSnapshot = replacement.PricingMode == "PER_DAY"
                ? ReservationExtraPricingMode.PerDay : ReservationExtraPricingMode.PerRental;
            selected.Locale = replacement.Locale;
            selected.RentalDaysSnapshot = replacement.RentalDays;
            selected.UnitPriceSnapshot = replacement.UnitPrice;
            selected.TotalPriceSnapshot = replacement.Total;
        }
        amendment.AppliedAt = DateTime.UtcNow;
        var response = new AmendmentResult(r.PublicCode, r.PickupDateTime, r.ReturnDateTime, r.TotalAmount);
        amendment.ResultJson = JsonSerializer.Serialize(response);
        db.AuditLogs.Add(new AuditLog { Action = "GuestReservationAmended", EntityType = "Reservation",
            EntityId = r.Id.ToString(), Details = JsonSerializer.Serialize(new { AmendmentId = amendment.Id, amendment.Fee }) });
        await UpdateRemindersAsync(r, access.Locale, false, ct);
        await notifications.EnqueueEmailAsync(Message(r, access.Locale, ChangedTemplate), cancellationToken: ct);
        await tx.CommitAsync(ct);
        invalidation.Invalidate();
        return response;
    }

    private async Task<decimal> PreviousAmendmentFeesAsync(Guid id, CancellationToken ct) =>
        await db.ReservationAmendments.Where(a => a.ReservationId == id && a.AppliedAt != null)
            .Select(a => (decimal?)a.Fee).SumAsync(ct) ?? 0;

    private async Task UpdateRemindersAsync(Reservation reservation, string locale, bool cancel, CancellationToken ct)
    {
        var abandonedBefore = DateTime.UtcNow.AddMinutes(-5);
        var jobs = await db.BackgroundJobs.AsNoTracking().Where(j =>
            (j.Status == BackgroundJobStatus.Pending ||
             (j.Status == BackgroundJobStatus.Processing && j.UpdatedAt < abandonedBefore)) &&
            j.Payload.Contains(reservation.PublicCode)).ToListAsync(ct);
        foreach (var job in jobs)
        {
            using var payload = JsonDocument.Parse(job.Payload);
            if (payload.RootElement.TryGetProperty("TemplateKey", out var key) &&
                key.GetString() is NotificationTemplateKeys.PickupReminder or NotificationTemplateKeys.ReturnReminder &&
                payload.RootElement.TryGetProperty("Variables", out var variables) &&
                variables.TryGetProperty("PublicCode", out var code) && code.GetString() == reservation.PublicCode)
                await db.BackgroundJobs.Where(j => j.Id == job.Id &&
                    (j.Status == BackgroundJobStatus.Pending ||
                     (j.Status == BackgroundJobStatus.Processing && j.UpdatedAt < abandonedBefore)))
                    .ExecuteUpdateAsync(update => update.SetProperty(j => j.Status, BackgroundJobStatus.Cancelled)
                        .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
        }
        if (cancel) return;
        foreach (var (key, scheduled) in new[] {
            (NotificationTemplateKeys.PickupReminder, reservation.PickupDateTime.AddHours(-24)),
            (NotificationTemplateKeys.ReturnReminder, reservation.ReturnDateTime.AddHours(-24)) })
        {
            if (scheduled <= DateTime.UtcNow) continue;
            var variables = new Dictionary<string, string> { ["PublicCode"] = reservation.PublicCode };
            await notifications.EnqueueEmailAsync(new() { ToEmail = reservation.Customer!.Email,
                Locale = locale, TemplateKey = key, Variables = variables }, scheduled, ct);
            if (!string.IsNullOrWhiteSpace(reservation.Customer.Phone))
                await notifications.EnqueueSmsAsync(new() { ToPhoneNumber = reservation.Customer.Phone,
                    Locale = locale, TemplateKey = key, Variables = variables }, scheduled, ct);
        }
    }

    private Task<Reservation> LoadAsync(Guid id, CancellationToken ct) => db.Reservations
        .Include(r => r.Customer).Include(r => r.Vehicle).Include(r => r.PickupOffice).Include(r => r.ReturnOffice)
        .Include(r => r.SelectedExtras).SingleAsync(r => r.Id == id, ct);

    private async Task LockReservationAsync(Guid id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM reservations WHERE id = {id} FOR UPDATE", ct);
        foreach (var entry in db.ChangeTracker.Entries<Reservation>().Where(e => e.Entity.Id == id))
            await entry.ReloadAsync(ct);
    }

    private async Task LockPolicyAsync(Reservation reservation, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM offices WHERE id = {reservation.PickupOfficeId} FOR SHARE", ct);
        if (reservation.PickupOffice is not null) await db.Entry(reservation.PickupOffice).ReloadAsync(ct);
    }

    public static bool CanManage(Reservation r, GuestManagementPolicy? policy, bool amendment)
    {
        if (r.Status != ReservationStatus.Confirmed || r.PricingSnapshot?.BookingConditions?.PaymentAtPickup != true || policy is null)
            return false;
        var enabled = amendment ? policy.AllowDateChange : policy.AllowCancellation;
        var notice = amendment ? policy.ChangeNoticeMinutes : policy.CancellationNoticeMinutes;
        var fee = amendment ? policy.ChangeFee : policy.CancellationFee;
        return enabled && notice is >= 0 and <= 525600 && fee is >= 0 and <= 1000000 &&
            r.PickupDateTime > DateTime.UtcNow && r.PickupDateTime >= DateTime.UtcNow.AddMinutes(notice.Value);
    }

    private static QueuedEmailNotificationRequest Message(Reservation r, string locale, string template,
        Dictionary<string, string>? variables = null)
    {
        variables ??= new();
        variables["PublicCode"] = r.PublicCode;
        variables["PickupDate"] = r.PickupDateTime.ToString("O");
        variables["ReturnDate"] = r.ReturnDateTime.ToString("O");
        variables["Total"] = r.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
        variables["Currency"] = r.PricingSnapshot?.Currency ?? "TRY";
        return new() { ToEmail = r.Customer!.Email, Locale = locale, TemplateKey = template, Variables = variables };
    }

    private static string Locale(string locale) => locale is "tr" or "en" or "de" or "ru" or "ar" ? locale : "tr";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool EqualHash(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    private sealed record AmendmentResult(string PublicCode, DateTime PickupDateTime, DateTime ReturnDateTime, decimal TotalAmount);
}
