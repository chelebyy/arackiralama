using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class GuestReservationTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task AccessLimits_SeparateSignedBffClientsAndRejectSpoofedPartitions()
    {
        for (var client = 0; client < 26; client++)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                using var request = SignedRequest(client, attempt % 2 == 0 ? "request" : "verify");
                using var response = await Client.SendAsync(request);
                response.StatusCode.Should().Be(attempt % 2 == 0 ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
            }
        }
        using var fifth = await Client.SendAsync(SignedRequest(0, "request"));
        fifth.StatusCode.Should().Be(HttpStatusCode.OK);
        using var sixth = await Client.SendAsync(SignedRequest(0, "verify"));
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        for (var i = 0; i < 6; i++)
        {
            using var forged = SignedRequest(i + 100, "request");
            forged.Headers.Remove("X-Guest-Signature");
            forged.Headers.Add("X-Guest-Signature", new string('0', 64));
            using var response = await Client.SendAsync(forged);
            response.StatusCode.Should().Be(i < 5 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests);
        }
    }

    private static HttpRequestMessage SignedRequest(int client, string action)
    {
        const string secret = "test-only-guest-proxy-secret-at-least-32-characters";
        var path = "/api/guest/v1/reservation/" + action;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var partition = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(client.ToString())));
        var signature = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret),
            System.Text.Encoding.UTF8.GetBytes($"{timestamp}\n{partition}\nPOST\n{path}")));
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = action == "request" ? JsonContent.Create(new GuestAccessRequest("UNKNOWN", "nobody@example.test")) :
                JsonContent.Create(new GuestVerificationRequest(Guid.NewGuid(), "INVALID"))
        };
        request.Headers.Add("X-Guest-Client", partition);
        request.Headers.Add("X-Guest-Timestamp", timestamp);
        request.Headers.Add("X-Guest-Signature", signature);
        return request;
    }

    private static readonly DriverDeclaration Declaration = new()
    {
        AgeAtPickup = 30, LicenseYearsAtPickup = 8,
        LicenseValidThroughReturn = true, DocumentsAvailableAtPickup = true
    };
    private static readonly GuestManagementPolicy Policy = new()
    {
        AllowCancellation = true, CancellationNoticeMinutes = 60, CancellationFee = 25,
        AllowDateChange = true, ChangeNoticeMinutes = 60, ChangeFee = 10
    };

    [Fact]
    public async Task Access_IsNeutralSingleUseBoundedAndRevocable()
    {
        var reservation = await SeedAsync();
        var challenge = await ChallengeAsync(reservation);
        var missing = await ServiceAsync(s => s.RequestAccessAsync(new("UNKNOWN", "nobody@example.test"), default));
        (await WithDbContextAsync(db => db.GuestReservationAccess.CountAsync())).Should().Be(1);
        (await ServiceAsync(s => s.VerifyAsync(new(missing, challenge.Code), default))).Should().BeNull();
        (await ServiceAsync(s => s.VerifyAsync(new(challenge.Id, "WRONG"), default))).Should().BeNull();
        var session = (await ServiceAsync(s => s.VerifyAsync(new(challenge.Id, challenge.Code), default)))!;
        session.Should().NotBeNull();
        (await ServiceAsync(s => s.VerifyAsync(new(challenge.Id, challenge.Code), default))).Should().BeNull();
        (await ServiceAsync(s => s.AuthenticateAsync(session.SessionToken, "BAD", true, default))).Should().BeNull();
        (await ServiceAsync(s => s.AuthenticateAsync(session.SessionToken, null, false, default)))!.ReservationId.Should().Be(reservation.Id);
        var stored = await WithDbContextAsync(db => db.GuestReservationAccess.SingleAsync());
        stored.CodeHash.Should().BeEmpty();
        stored.SessionHash.Should().NotBe(session.SessionToken);
        stored.CsrfHash.Should().NotBe(session.CsrfToken);
        using var unauthenticated = await Client.GetAsync("/api/guest/v1/reservation");
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var scanner = await Client.GetAsync("/api/guest/v1/reservation/verify");
        scanner.IsSuccessStatusCode.Should().BeFalse();
        await AuthorizedAsync(session, async (s, a) => { await s.LogoutAsync(a, default); return true; });
        (await ServiceAsync(s => s.AuthenticateAsync(session.SessionToken, session.CsrfToken, true, default))).Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Access_RejectsExpiredOrExhaustedCodes(bool expire)
    {
        var challenge = await ChallengeAsync(await SeedAsync());
        if (expire)
            await WithDbContextAsync(async db => { (await db.GuestReservationAccess.SingleAsync()).CodeExpiresAt = DateTime.UtcNow.AddSeconds(-1); return await db.SaveChangesAsync(); });
        else
            for (var i = 0; i < 5; i++)
                (await ServiceAsync(s => s.VerifyAsync(new(challenge.Id, "WRONG"), default))).Should().BeNull();
        (await ServiceAsync(s => s.VerifyAsync(new(challenge.Id, challenge.Code), default))).Should().BeNull();
    }

    [Fact]
    public async Task Cancellation_FailsClosedThenCommitsOnceWithAuditAndQueuedMail()
    {
        var reservation = await SeedAsync(false);
        var session = await SessionAsync(reservation);
        var blocked = () => AuthorizedAsync(session, (s, a) => s.CancelAsync(a, new(reservation.Version, 0), default));
        await blocked.Should().ThrowAsync<ReservationQuoteConflictException>();
        await ConfigurePolicyAsync(Policy);
        var wrongFee = () => AuthorizedAsync(session, (s, a) => s.CancelAsync(a, new(reservation.Version, 0), default));
        await wrongFee.Should().ThrowAsync<ReservationQuoteConflictException>();
        await AuthorizedAsync(session, (s, a) => s.CancelAsync(a, new(reservation.Version, 25), default));
        await AuthorizedAsync(session, (s, a) => s.CancelAsync(a, new(reservation.Version, 25), default));
        var saved = await ReadAsync(reservation.Id);
        saved.Status.Should().Be(ReservationStatus.Cancelled);
        saved.TotalAmount.Should().Be(reservation.TotalAmount);
        (await WithDbContextAsync(db => db.AuditLogs.CountAsync(l => l.Action == "GuestReservationCancelled"))).Should().Be(1);
        (await MailCountAsync(GuestReservationService.CancelledTemplate)).Should().Be(1);
        (await WithDbContextAsync(db => db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Cancelled))).Should().Be(1);
    }

    [Fact]
    public async Task Amendment_RepricesPreservesHistoryAndReplaysAfterResponseLoss()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var offer = await OfferAsync(session, original.PickupDateTime.AddDays(1), original.ReturnDateTime.AddDays(2));
        var confirmation = new GuestAmendmentConfirmation(offer.GetProperty("AmendmentId").GetGuid(), offer.GetProperty("FinalTotal").GetDecimal());
        await AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a, confirmation, default));
        await AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a, confirmation, default));
        var saved = await ReadAsync(original.Id);
        saved.PickupDateTime.Should().Be(original.PickupDateTime.AddDays(1));
        saved.ReturnDateTime.Should().Be(original.ReturnDateTime.AddDays(2));
        saved.OccupiedUntilUtc.Should().Be(saved.ReturnDateTime.AddMinutes(60));
        saved.TotalAmount.Should().Be(confirmation.AcceptedTotal);
        saved.VehicleId.Should().Be(original.VehicleId);
        saved.PricingSnapshot!.BookingConditions!.DriverDeclaration.Should().BeEquivalentTo(Declaration);
        var history = await WithDbContextAsync(db => db.ReservationAmendments.SingleAsync());
        history.AppliedAt.Should().NotBeNull();
        history.PreviousSnapshot.Should().Contain(original.PickupDateTime.ToString("yyyy-MM-dd"));
        (await MailCountAsync(GuestReservationService.ChangedTemplate)).Should().Be(1);
        (await WithDbContextAsync(db => db.AuditLogs.CountAsync(l => l.Action == "GuestReservationAmended"))).Should().Be(1);
        (await WithDbContextAsync(db => db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Cancelled))).Should().Be(1);
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("expired")]
    [InlineData("price")]
    [InlineData("collision")]
    [InlineData("amount")]
    public async Task Amendment_FailureKeepsOriginalAndSendsNoConfirmation(string failure)
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var pickup = original.PickupDateTime.AddDays(5);
        var offer = await OfferAsync(session, pickup, pickup.AddDays(3));
        var confirmation = new GuestAmendmentConfirmation(offer.GetProperty("AmendmentId").GetGuid(), offer.GetProperty("FinalTotal").GetDecimal());
        if (failure == "policy") await ConfigurePolicyAsync(Policy with { ChangeFee = 99 });
        if (failure == "expired") await WithDbContextAsync(async db => { (await db.ReservationAmendments.SingleAsync()).ExpiresAt = DateTime.UtcNow.AddSeconds(-1); return await db.SaveChangesAsync(); });
        if (failure == "price") await WithDbContextAsync(async db => { var v = await db.Vehicles.SingleAsync(v => v.Id == original.VehicleId); v.RentalTerms!.Rates[0].DailyPrice += 100; return await db.SaveChangesAsync(); });
        if (failure == "collision") await SeedAsync(true, 15);
        if (failure == "amount") confirmation = confirmation with { AcceptedTotal = confirmation.AcceptedTotal + 1 };
        var rejected = () => AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a, confirmation, default));
        await rejected.Should().ThrowAsync<ReservationQuoteConflictException>();
        var saved = await ReadAsync(original.Id);
        saved.PickupDateTime.Should().Be(original.PickupDateTime);
        saved.ReturnDateTime.Should().Be(original.ReturnDateTime);
        saved.TotalAmount.Should().Be(original.TotalAmount);
        (await MailCountAsync(GuestReservationService.ChangedTemplate)).Should().Be(0);
        (await WithDbContextAsync(db => db.ReservationAmendments.SingleAsync())).AppliedAt.Should().BeNull();
    }

    [Fact]
    public async Task Amendment_CannotBeAppliedByAnotherReservationSession()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var other = await SessionAsync(await SeedAsync(true, 25));
        var offer = await OfferAsync(session, original.PickupDateTime.AddDays(1), original.ReturnDateTime.AddDays(1));
        var rejected = () => AuthorizedAsync(other, (s, a) => s.ConfirmAmendmentAsync(a,
            new(offer.GetProperty("AmendmentId").GetGuid(), offer.GetProperty("FinalTotal").GetDecimal()), default));
        await rejected.Should().ThrowAsync<ReservationQuoteConflictException>();
        (await MailCountAsync(GuestReservationService.ChangedTemplate)).Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentAmendments_ApplyOnlyOneVersion()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var first = await OfferAsync(session, original.PickupDateTime.AddDays(1), original.ReturnDateTime.AddDays(1));
        var second = await OfferAsync(session, original.PickupDateTime.AddDays(2), original.ReturnDateTime.AddDays(2));
        async Task<bool> Apply(JsonElement offer)
        {
            try { await AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a,
                new(offer.GetProperty("AmendmentId").GetGuid(), offer.GetProperty("FinalTotal").GetDecimal()), default)); return true; }
            catch (ReservationQuoteConflictException) { return false; }
        }
        (await Task.WhenAll(Apply(first), Apply(second))).Count(x => x).Should().Be(1);
        (await MailCountAsync(GuestReservationService.ChangedTemplate)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentCancellationAndAmendment_HaveOneConsistentWinner()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var offer = await OfferAsync(session, original.PickupDateTime.AddDays(1), original.ReturnDateTime.AddDays(1));
        async Task<bool> Attempt(bool cancel)
        {
            try
            {
                await AuthorizedAsync(session, (s, a) => cancel
                    ? s.CancelAsync(a, new(original.Version, 25), default)
                    : s.ConfirmAmendmentAsync(a, new(offer.GetProperty("AmendmentId").GetGuid(), offer.GetProperty("FinalTotal").GetDecimal()), default));
                return true;
            }
            catch (ReservationQuoteConflictException) { return false; }
        }
        (await Task.WhenAll(Attempt(true), Attempt(false))).Count(x => x).Should().Be(1);
        ((await MailCountAsync(GuestReservationService.ChangedTemplate)) + (await MailCountAsync(GuestReservationService.CancelledTemplate))).Should().Be(1);
    }

    [Fact]
    public async Task RepeatedAmendment_PreservesPreviouslyAcceptedFees()
    {
        var original = await SeedAsync();
        var session = await SessionAsync(original);
        var first = await OfferAsync(session, original.PickupDateTime.AddDays(1), original.ReturnDateTime.AddDays(1));
        await AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a,
            new(first.GetProperty("AmendmentId").GetGuid(), first.GetProperty("FinalTotal").GetDecimal()), default));
        var second = await OfferAsync(session, original.PickupDateTime.AddDays(2), original.ReturnDateTime.AddDays(2));
        second.GetProperty("PreviousFees").GetDecimal().Should().Be(10);
        second.GetProperty("FinalTotal").GetDecimal().Should().Be(second.GetProperty("RentalTotal").GetDecimal() + 20);
        await AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a,
            new(second.GetProperty("AmendmentId").GetGuid(), second.GetProperty("FinalTotal").GetDecimal()), default));
        (await ReadAsync(original.Id)).TotalAmount.Should().Be(second.GetProperty("FinalTotal").GetDecimal());
    }

    [Fact]
    public async Task Amendment_QuotesCurrentExtraVersionAndPersistsAcceptedPrice()
    {
        var original = await SeedAsync();
        var extraId = await WithDbContextAsync(async db =>
        {
            var option = await db.ReservationExtraOptions.Include(e => e.VehicleGroups).OrderBy(e => e.SortOrder).FirstAsync();
            option.IsActive = true;
            option.IsArchived = false;
            if (option.VehicleGroups.All(g => g.VehicleGroupId != TestDataSeeder.GroupOneId))
                option.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup { OptionId = option.Id, VehicleGroupId = TestDataSeeder.GroupOneId });
            db.ReservationSelectedExtras.Add(new ReservationSelectedExtra { ReservationId = original.Id, ExtraOptionId = option.Id, OptionVersionSnapshot = option.Version,
                Quantity = 1, NameSnapshot = "Earlier price", Locale = "en", UnitPriceSnapshot = option.UnitPrice });
            option.UnitPrice += 15;
            await db.SaveChangesAsync();
            return option.Id;
        });
        var session = await SessionAsync(original);
        var offer = await OfferAsync(session, original.PickupDateTime.AddDays(1), original.ReturnDateTime.AddDays(1));
        await AuthorizedAsync(session, (s, a) => s.ConfirmAmendmentAsync(a,
            new(offer.GetProperty("AmendmentId").GetGuid(), offer.GetProperty("FinalTotal").GetDecimal()), default));
        await WithDbContextAsync(async db =>
        {
            var selected = await db.ReservationSelectedExtras.SingleAsync(e => e.ReservationId == original.Id);
            var current = await db.ReservationExtraOptions.SingleAsync(e => e.Id == extraId);
            selected.OptionVersionSnapshot.Should().Be(current.Version);
            selected.UnitPriceSnapshot.Should().Be(current.UnitPrice);
            selected.RentalDaysSnapshot.Should().Be(3);
            return 0;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MailFailure_RetriesWithoutUndoingCancellation(bool permanentFailure)
    {
        var reservation = await SeedAsync();
        var session = await SessionAsync(reservation);
        await WithDbContextAsync(async db =>
        {
            foreach (var job in await db.BackgroundJobs.Where(j => j.Payload.Contains(GuestReservationService.AccessTemplate)).ToListAsync())
                job.Status = BackgroundJobStatus.Completed;
            return await db.SaveChangesAsync();
        });
        await AuthorizedAsync(session, (s, a) => s.CancelAsync(a, new(reservation.Version, 25), default));
        var sink = new TestEmailSink { Fail = true };
        for (var attempt = 1; attempt <= (permanentFailure ? 3 : 2); attempt++)
        {
            if (!permanentFailure && attempt == 2) sink.Fail = false;
            await WithDbContextAsync(async db =>
            {
                var job = await db.BackgroundJobs.SingleAsync(j => j.Payload.Contains(GuestReservationService.CancelledTemplate));
                job.ScheduledAt = DateTime.UtcNow.AddSeconds(-1);
                return await db.SaveChangesAsync();
            });
            await ProcessMailAsync(sink);
        }
        var result = await WithDbContextAsync(db => db.BackgroundJobs.SingleAsync(j => j.Payload.Contains(GuestReservationService.CancelledTemplate)));
        result.Status.Should().Be(permanentFailure ? BackgroundJobStatus.Failed : BackgroundJobStatus.Completed);
        result.RetryCount.Should().Be(permanentFailure ? 3 : 1);
        result.LastError.Should().Be(permanentFailure ? "Guest notification delivery failed." : null);
        (await ReadAsync(reservation.Id)).Status.Should().Be(ReservationStatus.Cancelled);
        (await MailCountAsync(GuestReservationService.CancelledTemplate)).Should().Be(1);
        sink.Delivered.Should().Be(permanentFailure ? 0 : 1);
    }

    [Fact]
    public async Task MailWorkers_ClaimOnceAndRecoverAbandonedWork()
    {
        await ChallengeAsync(await SeedAsync());
        await WithDbContextAsync(async db =>
        {
            var job = await db.BackgroundJobs.SingleAsync(j => j.Payload.Contains(GuestReservationService.AccessTemplate));
            job.Status = BackgroundJobStatus.Processing;
            job.UpdatedAt = DateTime.UtcNow.AddMinutes(-6);
            return await db.SaveChangesAsync();
        });
        var sink = new TestEmailSink();
        await Task.WhenAll(ProcessMailAsync(sink), ProcessMailAsync(sink));
        sink.Delivered.Should().Be(1);
        (await WithDbContextAsync(db => db.BackgroundJobs.SingleAsync(j => j.Payload.Contains(GuestReservationService.AccessTemplate))))
            .Status.Should().Be(BackgroundJobStatus.Completed);
    }

    [Fact]
    public async Task HttpContract_VerifiesScopeAndRejectsMissingCsrf()
    {
        var reservation = await SeedAsync();
        using var requested = await Client.PostAsJsonAsync("/api/guest/v1/reservation/request",
            new GuestAccessRequest(reservation.PublicCode, reservation.Customer!.Email, "en"));
        requested.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await requested.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = body.GetProperty("challengeId").GetGuid();
        var job = await WithDbContextAsync(db => db.BackgroundJobs.SingleAsync(j => j.Payload.Contains(GuestReservationService.AccessTemplate)));
        var payload = JsonSerializer.Deserialize<QueuedEmailNotificationRequest>(job.Payload)!;
        var code = Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(GuestReservationService.MailPurpose)
            .Unprotect(payload.Variables["ProtectedCode"]);
        using var verified = await Client.PostAsJsonAsync("/api/guest/v1/reservation/verify", new GuestVerificationRequest(challengeId, code));
        verified.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await verified.Content.ReadFromJsonAsync<GuestSessionResult>())!;
        Client.DefaultRequestHeaders.Add("X-Guest-Session", session.SessionToken);
        using var view = await Client.GetAsync("/api/guest/v1/reservation");
        view.StatusCode.Should().Be(HttpStatusCode.OK);
        var viewBody = await view.Content.ReadAsStringAsync();
        viewBody.Should().NotContain(reservation.Customer.Email).And.NotContain("LicenseNumber").And.NotContain("EmailHash");
        using var denied = await Client.PostAsJsonAsync("/api/guest/v1/reservation/cancel", new GuestCancellationRequest(reservation.Version, 25));
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Client.DefaultRequestHeaders.Add("X-Guest-CSRF", session.CsrfToken);
        using var accepted = await Client.PostAsJsonAsync("/api/guest/v1/reservation/cancel", new GuestCancellationRequest(reservation.Version, 25));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<int> ProcessMailAsync(TestEmailSink sink)
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        return await new NotificationBackgroundJobProcessor(provider.GetRequiredService<RentACarDbContext>(),
            provider.GetRequiredService<INotificationTemplateService>(), sink,
            provider.GetRequiredService<ISmsProvider>(), NullLogger<NotificationBackgroundJobProcessor>.Instance)
            .ProcessPendingAsync();
    }

    private sealed class TestEmailSink : IEmailProvider
    {
        public bool Fail { get; set; }
        private int delivered;
        public int Delivered => delivered;
        public async Task<EmailSendResult> SendAsync(EmailMessageRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(40, cancellationToken);
            if (Fail) return new() { Success = false, FailureMessage = "Synthetic provider failure containing private detail" };
            Interlocked.Increment(ref delivered);
            return new() { Success = true, Provider = "LocalTestSink" };
        }
    }

    private async Task<Reservation> SeedAsync(bool configure = true, int offset = 10)
    {
        if (configure) await ConfigurePolicyAsync(Policy);
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var pickup = DateTime.UtcNow.Date.AddDays(offset).AddHours(10);
        var offer = await provider.GetRequiredService<ReservationQuoteService>().CreateAsync(new CreateReservationQuoteRequest
        {
            VehicleId = TestDataSeeder.GroupOneId, VehicleGroupId = TestDataSeeder.GroupOneId,
            PickupOfficeId = TestDataSeeder.OfficeOneId, ReturnOfficeId = TestDataSeeder.OfficeOneId,
            PickupDateTimeUtc = pickup, ReturnDateTimeUtc = pickup.AddDays(3), DriverAge = 30, Locale = "en"
        }, Guid.NewGuid().ToString(), default);
        var quote = (await provider.GetRequiredService<IReservationQuoteStore>().GetAsync(offer.QuoteId))!;
        quote.PricingSnapshot.BookingConditions!.DriverDeclaration = Declaration;
        var db = provider.GetRequiredService<RentACarDbContext>();
        var customer = new Customer { FullName = "Synthetic Guest", Email = $"guest-{Guid.NewGuid():N}@example.test", Phone = "+900000000000" };
        var r = new Reservation
        {
            PublicCode = Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(), Customer = customer,
            VehicleId = TestDataSeeder.GroupOneId, PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId, PickupDateTime = pickup, ReturnDateTime = pickup.AddDays(3),
            OccupiedUntilUtc = pickup.AddDays(3).AddMinutes(60), Status = ReservationStatus.Confirmed,
            PricingSnapshot = quote.PricingSnapshot, QuoteId = quote.QuoteId, TotalAmount = offer.FinalTotal
        };
        db.Reservations.Add(r);
        db.BackgroundJobs.Add(new BackgroundJob { Type = "notification.email.send", ScheduledAt = pickup.AddHours(-24),
            Payload = JsonSerializer.Serialize(new QueuedEmailNotificationRequest { ToEmail = customer.Email,
                TemplateKey = NotificationTemplateKeys.PickupReminder, Variables = new Dictionary<string, string> { ["PublicCode"] = r.PublicCode } }) });
        await db.SaveChangesAsync();
        return r;
    }

    private Task<int> ConfigurePolicyAsync(GuestManagementPolicy policy) => WithDbContextAsync(async db =>
    {
        var office = await db.Offices.SingleAsync(o => o.Id == TestDataSeeder.OfficeOneId);
        office.OperatingPolicy!.GuestManagement = policy;
        return await db.SaveChangesAsync();
    });
    private Task<Reservation> ReadAsync(Guid id) => WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
    private Task<int> MailCountAsync(string template) => WithDbContextAsync(db => db.BackgroundJobs.CountAsync(j => j.Payload.Contains(template)));
    private async Task<(Guid Id, string Code)> ChallengeAsync(Reservation r)
    {
        var id = await ServiceAsync(s => s.RequestAccessAsync(new(r.PublicCode, r.Customer!.Email, "en"), default));
        var job = await WithDbContextAsync(db => db.BackgroundJobs.SingleAsync(j => j.Payload.Contains(GuestReservationService.AccessTemplate) && j.Payload.Contains(r.Customer!.Email)));
        var request = JsonSerializer.Deserialize<QueuedEmailNotificationRequest>(job.Payload)!;
        var code = Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(GuestReservationService.MailPurpose).Unprotect(request.Variables["ProtectedCode"]);
        job.Payload.Should().NotContain(code);
        return (id, code);
    }
    private async Task<GuestSessionResult> SessionAsync(Reservation r)
    {
        var challenge = await ChallengeAsync(r);
        return (await ServiceAsync(s => s.VerifyAsync(new(challenge.Id, challenge.Code), default)))!;
    }
    private async Task<JsonElement> OfferAsync(GuestSessionResult session, DateTime pickup, DateTime end) =>
        JsonSerializer.SerializeToElement(await AuthorizedAsync(session, (s, a) => s.QuoteAmendmentAsync(a, new(pickup, end, Declaration), default)));
    private async Task<T> ServiceAsync<T>(Func<GuestReservationService, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<GuestReservationService>());
    }
    private Task<T> AuthorizedAsync<T>(GuestSessionResult session, Func<GuestReservationService, GuestReservationAccess, Task<T>> action) =>
        ServiceAsync(async s =>
        {
            var access = await s.AuthenticateAsync(session.SessionToken, session.CsrfToken, true, default);
            access.Should().NotBeNull();
            return await action(s, access!);
        });
}
