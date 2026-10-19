using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Data;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed partial class GuestReservationTests
{
    [Theory]
    [InlineData("utc")]
    [InlineData("unspecified")]
    [InlineData("offset")]
    public async Task AmendmentRegression_UnchangedNormalizedDatesRejectQuoteWithoutSideEffects(string representation)
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var before = await AmendmentStateAsync(original.Id);
        string Format(DateTime value) => representation switch
        {
            "unspecified" => DateTime.SpecifyKind(value, DateTimeKind.Unspecified).ToString("O"),
            "offset" => new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(3)).ToString("O"),
            _ => value.ToString("O")
        };
        using var response = await AmendmentPostAsync(session, "quote", new
        {
            PickupDateTimeUtc = Format(original.PickupDateTime),
            ReturnDateTimeUtc = Format(original.ReturnDateTime), Declaration
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("invalid_request");
        (await AmendmentStateAsync(original.Id)).Should().Be(before);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AmendmentRegression_OneBoundaryTimeChangeRemainsValidAndChargesOnce(bool changePickup)
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var pickup = changePickup ? original.PickupDateTime.AddMinutes(30) : original.PickupDateTime;
        var end = changePickup ? original.ReturnDateTime : original.ReturnDateTime.AddMinutes(30);
        var confirmation = await AmendmentHttpOfferAsync(session, pickup, end);
        using var response = await AmendmentPostAsync(session, "confirm", confirmation);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadAsync(original.Id);
        saved.PickupDateTime.Should().Be(pickup);
        saved.ReturnDateTime.Should().Be(end);
        saved.TotalAmount.Should().Be(confirmation.AcceptedTotal);
        var amendment = await WithDbContextAsync(db => db.ReservationAmendments.AsNoTracking().SingleAsync());
        amendment.Fee.Should().Be(10);
        amendment.AppliedAt.Should().NotBeNull();
        var beforeReplay = await AmendmentStateAsync(original.Id);
        using var replay = await AmendmentPostAsync(session, "confirm", confirmation);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AmendmentStateAsync(original.Id)).Should().Be(beforeReplay);
    }

    [Fact]
    public async Task AmendmentRegression_PreexistingPendingNoOpQuoteRejectsConfirmationWithoutSideEffects()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var confirmation = await SeedUnchangedAmendmentAsync(original, session, false);
        var before = await AmendmentStateAsync(original.Id);
        using var response = await AmendmentPostAsync(session, "confirm", confirmation);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("invalid_request");
        (await AmendmentStateAsync(original.Id)).Should().Be(before);
        var amendment = await WithDbContextAsync(db => db.ReservationAmendments.AsNoTracking().SingleAsync());
        amendment.AppliedAt.Should().BeNull();
        amendment.ResultJson.Should().BeNull();
    }

    [Fact]
    public async Task AmendmentRegression_AlreadyAppliedNoOpConfirmationStillReplaysWithoutSideEffects()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var confirmation = await SeedUnchangedAmendmentAsync(original, session, true);
        var before = await AmendmentStateAsync(original.Id);
        using var response = await AmendmentPostAsync(session, "confirm", confirmation);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("pickupDateTime").GetDateTime().Should().Be(original.PickupDateTime);
        result.GetProperty("returnDateTime").GetDateTime().Should().Be(original.ReturnDateTime);
        result.GetProperty("totalAmount").GetDecimal().Should().Be(confirmation.AcceptedTotal);
        (await AmendmentStateAsync(original.Id)).Should().Be(before);
    }

    [Fact]
    public Task AmendmentRegression_FreshProcessingRemindersDoNotBlockRepeatedDatesAndRecoverSafely() =>
        RepeatedAmendmentHttpAsync(true, 10);

    [Fact]
    public Task AmendmentRegression_ZeroFeeRepeatedDatesDoNotCollideWithEarlierPendingConfirmationMail() =>
        RepeatedAmendmentHttpAsync(false, 0);

    private async Task RepeatedAmendmentHttpAsync(bool retainProcessingReminders, decimal changeFee)
    {
        var original = await SeedAsync();
        await ConfigurePolicyAsync(Policy with { ChangeFee = changeFee });
        var session = await SessionAsync(original);
        var amendments = new List<Guid>();
        var oldReminderIds = new List<Guid>();
        foreach (var offset in new[] { 1, 2, 1 })
        {
            var confirmation = await AmendmentHttpOfferAsync(session,
                original.PickupDateTime.AddDays(offset), original.ReturnDateTime.AddDays(offset));
            using var response = await AmendmentPostAsync(session, "confirm", confirmation);
            response.StatusCode.Should().Be(HttpStatusCode.OK, "a distinct amendment must not collide with an earlier active notification");
            amendments.Add(confirmation.AmendmentId);
            var beforeReplay = await AmendmentStateAsync(original.Id);
            using var replay = await AmendmentPostAsync(session, "confirm", confirmation);
            replay.StatusCode.Should().Be(HttpStatusCode.OK);
            (await AmendmentStateAsync(original.Id)).Should().Be(beforeReplay);
            if (retainProcessingReminders && amendments.Count < 3)
            {
                var claimed = await WithDbContextAsync(async db =>
                {
                    var reminders = await db.BackgroundJobs.Where(j => j.Status == BackgroundJobStatus.Pending &&
                        (j.Payload.Contains(NotificationTemplateKeys.PickupReminder) ||
                         j.Payload.Contains(NotificationTemplateKeys.ReturnReminder))).ToListAsync();
                    reminders.Should().HaveCount(4);
                    foreach (var job in reminders)
                    {
                        job.Status = BackgroundJobStatus.Processing;
                        job.ScheduledAt = DateTime.UtcNow.AddSeconds(-1);
                        job.UpdatedAt = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync();
                    return reminders.Select(j => j.Id).ToArray();
                });
                oldReminderIds.AddRange(claimed);
            }
        }
        var applied = await WithDbContextAsync(db => db.ReservationAmendments.AsNoTracking().ToListAsync());
        applied.Should().HaveCount(3).And.OnlyContain(a => a.AppliedAt.HasValue);
        applied.Sum(a => a.Fee).Should().Be(3 * changeFee);
        (await WithDbContextAsync(db => db.AuditLogs.CountAsync(a => a.Action == "GuestReservationAmended"))).Should().Be(3);
        var changed = await WithDbContextAsync(db => db.BackgroundJobs.AsNoTracking()
            .Where(j => j.Payload.Contains(GuestReservationService.ChangedTemplate)).ToListAsync());
        changed.Should().HaveCount(3).And.OnlyContain(j => j.Status == BackgroundJobStatus.Pending);
        changed.Select(j => j.Payload).Should().OnlyHaveUniqueItems();
        foreach (var job in changed)
        {
            using var payload = JsonDocument.Parse(job.Payload);
            amendments.Should().Contain(Guid.Parse(payload.RootElement.GetProperty("Variables").GetProperty("AmendmentId").GetString()!));
        }
        var remindersAfterCommit = await WithDbContextAsync(db => db.BackgroundJobs.AsNoTracking().Where(j =>
            j.Payload.Contains(NotificationTemplateKeys.PickupReminder) ||
            j.Payload.Contains(NotificationTemplateKeys.ReturnReminder)).ToListAsync());
        remindersAfterCommit.Where(j => oldReminderIds.Contains(j.Id)).Should().OnlyContain(j => j.Status == BackgroundJobStatus.Processing);
        var replacements = remindersAfterCommit.Where(j => j.Status == BackgroundJobStatus.Pending).ToArray();
        replacements.Should().HaveCount(4);
        foreach (var job in replacements)
        {
            using var payload = JsonDocument.Parse(job.Payload);
            payload.RootElement.GetProperty("Variables").GetProperty("AmendmentId").GetString().Should().Be(amendments[^1].ToString("D"));
            job.CreatedAt.Should().BeOnOrAfter(applied.Single(a => a.Id == amendments[^1]).AppliedAt!.Value);
        }
        if (!retainProcessingReminders) return;
        await WithDbContextAsync(async db =>
        {
            foreach (var job in await db.BackgroundJobs.Where(j => oldReminderIds.Contains(j.Id)).ToListAsync())
                job.UpdatedAt = DateTime.UtcNow.AddMinutes(-6);
            await db.BackgroundJobs.Where(j => !oldReminderIds.Contains(j.Id) && j.ScheduledAt <= DateTime.UtcNow)
                .ExecuteUpdateAsync(update => update.SetProperty(j => j.Status, BackgroundJobStatus.Completed));
            return await db.SaveChangesAsync();
        });
        var emailSink = new TestEmailSink();
        var smsSink = new TestSmsSink();
        (await ProcessMailAsync(emailSink, smsSink)).Should().Be(0);
        emailSink.Delivered.Should().Be(0);
        smsSink.Delivered.Should().Be(0);
        var recovered = await WithDbContextAsync(db => db.BackgroundJobs.AsNoTracking()
            .Where(j => oldReminderIds.Contains(j.Id)).ToListAsync());
        recovered.Should().HaveCount(8).And.OnlyContain(j => j.Status == BackgroundJobStatus.Cancelled && j.RetryCount == 0);
        var replacementIds = replacements.Select(j => j.Id).ToArray();
        await WithDbContextAsync(db => db.BackgroundJobs.Where(j => replacementIds.Contains(j.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(j => j.ScheduledAt, DateTime.UtcNow.AddSeconds(-1))));
        (await ProcessMailAsync(emailSink, smsSink)).Should().Be(4);
        emailSink.Delivered.Should().Be(2);
        smsSink.Delivered.Should().Be(2);
        (await ProcessMailAsync(emailSink, smsSink)).Should().Be(0);
    }

    private async Task<GuestAmendmentConfirmation> AmendmentHttpOfferAsync(GuestSessionResult session, DateTime pickup, DateTime end)
    {
        using var response = await AmendmentPostAsync(session, "quote", new GuestAmendmentRequest(pickup, end, Declaration));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var offer = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(offer.GetProperty("amendmentId").GetGuid(), offer.GetProperty("finalTotal").GetDecimal());
    }

    private async Task<HttpResponseMessage> AmendmentPostAsync<T>(GuestSessionResult session, string action, T body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/guest/v1/reservation/amendment/" + action)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Guest-Session", session.SessionToken);
        request.Headers.Add("X-Guest-CSRF", session.CsrfToken);
        return await Client.SendAsync(request);
    }

    private Task<string> AmendmentStateAsync(Guid reservationId) => WithDbContextAsync(async db =>
        JsonSerializer.Serialize(new
        {
            Reservation = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId),
            Amendments = await db.ReservationAmendments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync(),
            Jobs = await db.BackgroundJobs.AsNoTracking().OrderBy(j => j.Id).ToArrayAsync(),
            Audits = await db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()
        }));

    private async Task<GuestAmendmentConfirmation> SeedUnchangedAmendmentAsync(
        Reservation original, GuestSessionResult session, bool alreadyApplied)
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<RentACarDbContext>();
        var access = (await provider.GetRequiredService<GuestReservationService>()
            .AuthenticateAsync(session.SessionToken, session.CsrfToken, true, default))!;
        var r = await db.Reservations.Include(r => r.PickupOffice).SingleAsync(r => r.Id == original.Id);
        var groupId = await db.Vehicles.Where(v => v.Id == r.VehicleId).Select(v => v.GroupId).SingleAsync();
        var offer = await provider.GetRequiredService<ReservationQuoteService>().CreateAmendmentAsync(new CreateReservationQuoteRequest
        {
            VehicleId = r.VehicleId, VehicleGroupId = groupId!.Value,
            PickupOfficeId = r.PickupOfficeId, ReturnOfficeId = r.ReturnOfficeId,
            PickupDateTimeUtc = r.PickupDateTime, ReturnDateTimeUtc = r.ReturnDateTime,
            DriverAge = Declaration.AgeAtPickup, Locale = access.Locale
        }, access.Id.ToString("N"), r.Id, default);
        var policy = r.PickupOffice!.OperatingPolicy!.GuestManagement!;
        var amendment = new ReservationAmendment
        {
            ReservationId = r.Id, AccessId = access.Id, QuoteId = offer.QuoteId,
            ReservationVersion = r.Version,
            RequestJson = JsonSerializer.Serialize(new GuestAmendmentRequest(r.PickupDateTime, r.ReturnDateTime, Declaration)),
            PolicyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(policy)))),
            Fee = policy.ChangeFee!.Value, ExpiresAt = offer.ExpiresAtUtc
        };
        var acceptedTotal = offer.FinalTotal + amendment.Fee;
        if (alreadyApplied)
        {
            var quote = (await provider.GetRequiredService<IReservationQuoteStore>().GetAsync(offer.QuoteId))!;
            r.PricingSnapshot = quote.PricingSnapshot;
            r.PricingSnapshot.OtherFees += amendment.Fee;
            r.PricingSnapshot.FinalTotal = acceptedTotal;
            r.TotalAmount = acceptedTotal;
            amendment.AppliedAt = DateTime.UtcNow;
            amendment.ResultJson = JsonSerializer.Serialize(new { r.PublicCode, r.PickupDateTime, r.ReturnDateTime, TotalAmount = acceptedTotal });
        }
        db.ReservationAmendments.Add(amendment);
        await db.SaveChangesAsync();
        return new(amendment.Id, acceptedTotal);
    }
}
