using System.Net;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Constants;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using StackExchange.Redis;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class ReservationQuoteEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuoteAndReservationCreate_PersistSnapshotAndReplayByQuoteId(bool exact)
    {
        var option = await WithDbContextAsync(async dbContext =>
        {
            var item = await dbContext.ReservationExtraOptions
                .Include(extra => extra.VehicleGroups)
                .OrderBy(extra => extra.SortOrder)
                .FirstAsync();
            item.IsActive = true;
            item.IsArchived = false;
            if (item.VehicleGroups.All(group => group.VehicleGroupId != TestDataSeeder.GroupOneId))
            {
                item.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup
                {
                    OptionId = item.Id,
                    VehicleGroupId = TestDataSeeder.GroupOneId
                });
            }
            await dbContext.SaveChangesAsync();
            return (item.Id, item.Version);
        });
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var sessionId = $"quote-session-{Guid.NewGuid():N}";
        var quoteRequest = new CreateReservationQuoteRequest
        {
            VehicleId = exact ? TestDataSeeder.GroupOneId : null,
            VehicleGroupId = TestDataSeeder.GroupOneId,
            PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            Locale = "tr",
            DriverAge = 30,
            SelectedExtras =
            [
                new SelectedReservationExtraInput
                {
                    OptionId = option.Id,
                    OptionVersion = option.Version,
                    Quantity = 1
                }
            ]
        };
        using var quoteMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing/quote")
        {
            Content = JsonContent.Create(quoteRequest)
        };
        quoteMessage.Headers.Add("X-Session-Id", sessionId);

        var quoteResponse = await Client.SendAsync(quoteMessage);
        var quoteBody = await quoteResponse.Content.ReadAsStringAsync();
        quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK, quoteBody);
        quoteResponse.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var quoteJson = JsonDocument.Parse(quoteBody);
        var quoteData = quoteJson.RootElement.GetProperty("data");
        var quoteId = quoteData.GetProperty("quoteId").GetGuid();
        quoteData.TryGetProperty("priceBreakdown", out _).Should().BeFalse();
        quoteData.GetProperty("extraItems").GetArrayLength().Should().Be(1);

        var reservationRequest = new CreateReservationRequest
        {
            VehicleId = quoteRequest.VehicleId,
            VehicleGroupId = quoteRequest.VehicleGroupId,
            PickupOfficeId = quoteRequest.PickupOfficeId,
            ReturnOfficeId = quoteRequest.ReturnOfficeId,
            PickupDateTimeUtc = quoteRequest.PickupDateTimeUtc,
            ReturnDateTimeUtc = quoteRequest.ReturnDateTimeUtc,
            QuoteId = quoteId,
            Locale = quoteRequest.Locale,
            DriverAge = quoteRequest.DriverAge,
            Driver = new DriverInfoRequest { Declaration = Declaration(30) },
            Customer = new CustomerInfoRequest
            {
                FirstName = "Quote",
                LastName = "Integration",
                Email = $"quote-{Guid.NewGuid():N}@rentacar.test",
                Phone = "+90 555 000 00 09"
                ,DateOfBirth = pickup.AddYears(-30), DriverLicenseIssueDate = pickup.AddYears(-5)
            }
        };
        var originalKey = $"idem-{Guid.NewGuid():N}";
        var firstResponse = await SendReservationAsync(reservationRequest, sessionId, originalKey);
        var secondResponse = await SendReservationAsync(reservationRequest, sessionId, $"idem-{Guid.NewGuid():N}");
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, firstBody);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, secondBody);
        using var firstJson = JsonDocument.Parse(firstBody);
        using var secondJson = JsonDocument.Parse(secondBody);
        var reservationId = firstJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        secondJson.RootElement.GetProperty("data").GetProperty("id").GetGuid().Should().Be(reservationId);

        var persisted = await WithDbContextAsync(async dbContext =>
            await dbContext.Reservations
                .AsNoTracking()
                .Include(reservation => reservation.SelectedExtras)
                .SingleAsync(reservation => reservation.QuoteId == quoteId));
        persisted.Id.Should().Be(reservationId);
        persisted.QuoteReplayProof!.SchemaVersion.Should().Be(exact ? 3 : 1);
        persisted.QuoteReplayProof.VehicleId.Should().Be(quoteRequest.VehicleId);
        persisted.PricingSnapshot.Should().NotBeNull();
        persisted.PricingSnapshot!.FinalTotal.Should().Be(persisted.TotalAmount);
        persisted.SelectedExtras.Should().ContainSingle();

        using var sameKeyReplay = await SendReservationAsync(reservationRequest, sessionId, originalKey);
        sameKeyReplay.StatusCode.Should().Be(HttpStatusCode.OK);
        using var changedSession = await SendReservationAsync(reservationRequest, "different-session", originalKey);
        changedSession.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var changedVehicle = await SendReservationAsync(reservationRequest with { VehicleId = Guid.NewGuid() }, sessionId, originalKey);
        changedVehicle.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ConcurrentExactVehicleUnpaidRequests_ReserveOnceWithoutSubstitution()
    {
        var alternativeId = await WithDbContextAsync(async dbContext =>
        {
            var alternative = new Vehicle
            {
                Plate = "EXACT-ALTERNATIVE", Brand = "Test", Model = "Alternative",
                GroupId = TestDataSeeder.GroupOneId, OfficeId = TestDataSeeder.OfficeOneId
            };
            dbContext.Vehicles.Add(alternative);
            await dbContext.SaveChangesAsync();
            return alternative.Id;
        });
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var selections = new List<(CreateReservationRequest Request, string Session)>();
        for (var index = 0; index < 2; index++)
        {
            var session = $"exact-{Guid.NewGuid():N}";
            var input = new CreateReservationQuoteRequest
            {
                VehicleId = TestDataSeeder.GroupOneId,
                VehicleGroupId = TestDataSeeder.GroupOneId,
                PickupOfficeId = TestDataSeeder.OfficeOneId,
                ReturnOfficeId = TestDataSeeder.OfficeOneId,
                PickupDateTimeUtc = pickup,
                ReturnDateTimeUtc = pickup.AddDays(3)
                ,DriverAge = 30
            };
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing/quote")
            {
                Content = JsonContent.Create(input)
            };
            message.Headers.Add("X-Session-Id", session);
            using var response = await Client.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("data").GetProperty("vehicleId").GetGuid().Should().Be(input.VehicleId!.Value);
            selections.Add((new CreateReservationRequest
            {
                VehicleId = input.VehicleId,
                VehicleGroupId = input.VehicleGroupId,
                PickupOfficeId = input.PickupOfficeId,
                ReturnOfficeId = input.ReturnOfficeId,
                PickupDateTimeUtc = input.PickupDateTimeUtc,
                ReturnDateTimeUtc = input.ReturnDateTimeUtc,
                QuoteId = json.RootElement.GetProperty("data").GetProperty("quoteId").GetGuid(),
                DriverAge = 30,
                Driver = new DriverInfoRequest { Declaration = Declaration(30) },
                Customer = new CustomerInfoRequest
                {
                    FirstName = "Synthetic", LastName = "Exact",
                    DateOfBirth = pickup.AddYears(-30), DriverLicenseIssueDate = pickup.AddYears(-5),
                    Email = $"exact-{Guid.NewGuid():N}@example.test", Phone = "+900000000000"
                }
            }, session));
        }

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = selections.Select(async selection =>
        {
            await barrier.Task;
            return await SendReservationAsync(selection.Request, selection.Session, Guid.NewGuid().ToString("N"), true);
        }).ToArray();
        barrier.SetResult();
        var responses = await Task.WhenAll(requests);
        responses.Select(response => response.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var winner = selections[responses[0].IsSuccessStatusCode ? 0 : 1];
        var booked = await WithDbContextAsync(dbContext => dbContext.Reservations.AsNoTracking().ToListAsync());
        booked.Should().ContainSingle().Which.VehicleId.Should().Be(TestDataSeeder.GroupOneId);
        booked[0].QuoteReplayProof!.VehicleId.Should().Be(TestDataSeeder.GroupOneId);
        booked[0].Status.Should().Be(RentACar.Core.Enums.ReservationStatus.Confirmed);
        booked[0].UnpaidRequestExpiresAtUtc.Should().BeNull();
        booked[0].OccupiedUntilUtc.Should().Be(pickup.AddDays(3).AddHours(1));

        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.FindAsync(TestDataSeeder.GroupOneId);
            vehicle!.Status = RentACar.Core.Enums.VehicleStatus.Maintenance;
            Func<Task> changeStatus = () => db.SaveChangesAsync();
            await changeStatus.Should().ThrowAsync<DbUpdateException>();
            return true;
        });

        using var replay = await SendReservationAsync(winner.Request, winner.Session, Guid.NewGuid().ToString("N"), true);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        using var tampered = await SendReservationAsync(winner.Request with { VehicleId = alternativeId },
            winner.Session, Guid.NewGuid().ToString("N"), true);
        tampered.StatusCode.Should().Be(HttpStatusCode.Conflict);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task MissingOfficePolicy_PreventsExactQuote()
    {
        await WithDbContextAsync(async db =>
        {
            var office = await db.Offices.FindAsync(TestDataSeeder.OfficeOneId);
            office!.OperatingPolicy = null;
            await db.SaveChangesAsync();
            return true;
        });
        using var response = await SendExactQuoteAsync(ExactInput(), Guid.NewGuid().ToString());
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("conditions")]
    [InlineData("policy")]
    public async Task ChangedContract_RequiresNewQuote(string change)
    {
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quoteResponse = await SendExactQuoteAsync(input, session);
        quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK, await quoteResponse.Content.ReadAsStringAsync());
        var quoteId = await QuoteIdAsync(quoteResponse);
        await WithDbContextAsync(async db =>
        {
            if (change == "policy")
            {
                var office = await db.Offices.FindAsync(input.PickupOfficeId);
                office!.OperatingPolicy!.PreparationMinutes = 120;
            }
            else
            {
                var vehicle = await db.Vehicles.FindAsync(input.VehicleId);
                if (change == "price") vehicle!.RentalTerms!.Rates[0].DailyPrice += 100;
                else vehicle!.RentalTerms!.MinLicenseYears += 1;
            }
            await db.SaveChangesAsync();
            return true;
        });
        using var stale = await SendReservationAsync(ExactReservation(input, quoteId), session, Guid.NewGuid().ToString(), true);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
        using var freshQuote = await SendExactQuoteAsync(input, session);
        freshQuote.StatusCode.Should().Be(HttpStatusCode.OK);
        using var fresh = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(freshQuote)), session, Guid.NewGuid().ToString(), true);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK, await fresh.Content.ReadAsStringAsync());
        (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task GrouplessVehicle_ConfirmsAndProtectsPreparationBoundary()
    {
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.FindAsync(TestDataSeeder.GroupOneId);
            vehicle!.GroupId = null;
            await db.SaveChangesAsync();
            return true;
        });
        var input = ExactInput() with { VehicleGroupId = Guid.Empty };
        var session = Guid.NewGuid().ToString();
        using var quoted = await SendExactQuoteAsync(input, session);
        quoted.StatusCode.Should().Be(HttpStatusCode.OK, await quoted.Content.ReadAsStringAsync());
        using var created = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quoted)), session, Guid.NewGuid().ToString(), true);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        using var overlap = await SendExactQuoteAsync(input with
        {
            PickupDateTimeUtc = input.ReturnDateTimeUtc.AddMinutes(59), ReturnDateTimeUtc = input.ReturnDateTimeUtc.AddDays(2)
        }, session);
        overlap.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var boundary = await SendExactQuoteAsync(input with
        {
            PickupDateTimeUtc = input.ReturnDateTimeUtc.AddMinutes(60), ReturnDateTimeUtc = input.ReturnDateTimeUtc.AddDays(2)
        }, session);
        boundary.StatusCode.Should().Be(HttpStatusCode.OK, await boundary.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        saved.Status.Should().Be(RentACar.Core.Enums.ReservationStatus.Confirmed);
        saved.TotalAmount.Should().Be(3000m);
        saved.PricingSnapshot!.BookingConditions!.PaymentAtPickup.Should().BeTrue();
        (await WithDbContextAsync(db => db.PaymentIntents.CountAsync())).Should().Be(0);
        await WithDbContextAsync(async db =>
        {
            var reservation = await db.Reservations.SingleAsync();
            reservation.CreatedAt = DateTime.UtcNow.AddHours(-25);
            await db.SaveChangesAsync();
            return true;
        });
        using var scope = Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReservationService>();
        await service.ProcessExpiredReservationsAsync();
        (await service.ExpireReservationAsync(saved.Id)).Should().BeNull();
        (await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync())).Status
            .Should().Be(RentACar.Core.Enums.ReservationStatus.Confirmed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrouplessVehicle_CampaignEligibilityUsesAuthoritativeQuote(bool restricted)
    {
        var input = ExactInput() with { VehicleGroupId = Guid.Empty, CampaignCode = "GLOBAL10" };
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.FindAsync(input.VehicleId);
            vehicle!.GroupId = null;
            db.Campaigns.Add(new Campaign
            {
                Code = input.CampaignCode, DiscountType = "percentage", DiscountValue = 10,
                MinDays = 1, ValidFrom = DateOnly.FromDateTime(input.PickupDateTimeUtc).AddDays(-1),
                ValidUntil = DateOnly.FromDateTime(input.ReturnDateTimeUtc).AddDays(1),
                AllowedVehicleGroupIds = restricted ? [TestDataSeeder.GroupOneId] : []
            });
            await db.SaveChangesAsync();
            return true;
        });
        var session = Guid.NewGuid().ToString();
        using var quoted = await SendExactQuoteAsync(input, session);
        var body = await quoted.Content.ReadAsStringAsync();
        if (restricted)
        {
            quoted.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
            body.Should().Contain("Campaign code is invalid or expired.");
            (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(0);
            return;
        }

        quoted.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("appliedCampaignCode").GetString().Should().Be(input.CampaignCode);
        data.GetProperty("campaignDiscount").GetDecimal().Should().Be(300m);
        data.GetProperty("finalTotal").GetDecimal().Should().Be(2700m);
        var request = ExactReservation(input, await QuoteIdAsync(quoted)) with { CampaignCode = input.CampaignCode };
        using var created = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), true);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ReservationStatus.Confirmed);
        saved.TotalAmount.Should().Be(2700m);
        saved.PricingSnapshot!.CampaignCode.Should().Be(input.CampaignCode);
        saved.PricingSnapshot.DiscountTotal.Should().Be(300m);
    }

    [Fact]
    public async Task DatedCatalogue_UsesExactPriceAndExcludesUnavailableVehicle()
    {
        var input = ExactInput();
        var query = $"/api/v1/vehicles/available-exact?pickupOfficeId={input.PickupOfficeId}&returnOfficeId={input.ReturnOfficeId}" +
            $"&pickupDateTimeUtc={Uri.EscapeDataString(input.PickupDateTimeUtc.ToString("O"))}&returnDateTimeUtc={Uri.EscapeDataString(input.ReturnDateTimeUtc.ToString("O"))}&driverAge=30";
        using var available = await Client.GetAsync(query);
        available.StatusCode.Should().Be(HttpStatusCode.OK, await available.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await available.Content.ReadAsStringAsync());
        var offer = json.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("vehicle").GetProperty("id").GetGuid() == input.VehicleId);
        offer.GetProperty("finalTotal").GetDecimal().Should().Be(3000m);
        offer.GetProperty("vehicle").TryGetProperty("plate", out _).Should().BeFalse();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        using var created = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quote)), session, Guid.NewGuid().ToString(), true);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        using var unavailable = await Client.GetAsync(query);
        unavailable.StatusCode.Should().Be(HttpStatusCode.OK);
        using var after = JsonDocument.Parse(await unavailable.Content.ReadAsStringAsync());
        after.RootElement.GetProperty("data").EnumerateArray().Should().NotContain(item =>
            item.GetProperty("vehicle").GetProperty("id").GetGuid() == input.VehicleId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GrouplessDraft_HoldRevalidatesAcceptedPolicy(bool changed, bool expired)
    {
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.FindAsync(TestDataSeeder.GroupOneId);
            vehicle!.GroupId = null;
            await db.SaveChangesAsync();
            return true;
        });
        var input = ExactInput() with { VehicleGroupId = Guid.Empty };
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        using var draft = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quote)), session, Guid.NewGuid().ToString());
        draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        if (changed)
        {
            await WithDbContextAsync(async db =>
            {
                var office = await db.Offices.FindAsync(input.PickupOfficeId);
                office!.OperatingPolicy!.PreparationMinutes = 120;
                await db.SaveChangesAsync();
                return true;
            });
        }
        if (expired)
        {
            await WithDbContextAsync(async db =>
            {
                var reservation = await db.Reservations.FindAsync(id);
                reservation!.PricingSnapshot!.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
                return true;
            });
        }
        using var holdRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{id}/hold");
        holdRequest.Headers.Add("X-Session-Id", session);
        using var hold = await Client.SendAsync(holdRequest);
        hold.StatusCode.Should().Be(changed || expired ? HttpStatusCode.Conflict : HttpStatusCode.OK, await hold.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        saved.Status.Should().Be(changed || expired ? ReservationStatus.Draft : ReservationStatus.Hold);
        if (changed || expired)
        {
            using var freshQuote = await SendExactQuoteAsync(input, session);
            freshQuote.StatusCode.Should().Be(HttpStatusCode.OK, await freshQuote.Content.ReadAsStringAsync());
            using var freshDraft = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(freshQuote)), session, Guid.NewGuid().ToString());
            freshDraft.StatusCode.Should().Be(HttpStatusCode.OK, await freshDraft.Content.ReadAsStringAsync());
            using var freshJson = JsonDocument.Parse(await freshDraft.Content.ReadAsStringAsync());
            var freshId = freshJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
            freshId.Should().NotBe(id);
            using var freshHoldRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{freshId}/hold");
            freshHoldRequest.Headers.Add("X-Session-Id", session);
            using var freshHold = await Client.SendAsync(freshHoldRequest);
            freshHold.StatusCode.Should().Be(HttpStatusCode.OK, await freshHold.Content.ReadAsStringAsync());
            var reservations = await WithDbContextAsync(db => db.Reservations.AsNoTracking().Where(r => r.Id == id || r.Id == freshId).ToListAsync());
            reservations.Should().ContainSingle(r => r.Status == ReservationStatus.Hold && r.Id == freshId);
            reservations.Should().ContainSingle(r => r.Status == ReservationStatus.Draft && r.Id == id);
        }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("cached")]
    [InlineData("expired-quote")]
    [InlineData("expired-license")]
    [InlineData("missing-license")]
    [InlineData("policy-changed")]
    [InlineData("wrong-session")]
    [InlineData("unknown-version")]
    [InlineData("v3-missing-declaration")]
    public async Task LegacyExactDraft_HoldKeepsStoredEligibilityAndSessionChecks(string scenario)
    {
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        using var draft = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quote)), session, Guid.NewGuid().ToString());
        draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        await WithDbContextAsync(async db =>
        {
            var reservation = await db.Reservations.SingleAsync(r => r.Id == id);
            reservation.QuoteReplayProof!.SchemaVersion.Should().Be(3);
            reservation.QuoteReplayProof!.SchemaVersion = scenario == "unknown-version" ? 4 : scenario == "v3-missing-declaration" ? 3 : 2;
            reservation.PricingSnapshot!.BookingConditions!.DriverDeclaration = null;
            reservation.DriverDateOfBirth = input.PickupDateTimeUtc.AddYears(-30);
            reservation.DriverLicenseIssueDate = scenario == "missing-license" ? null : input.PickupDateTimeUtc.AddYears(-8);
            reservation.DriverLicenseExpiryDate = scenario == "expired-license" ? input.PickupDateTimeUtc.AddDays(-1) : input.ReturnDateTimeUtc.AddYears(1);
            if (scenario == "expired-quote") reservation.PricingSnapshot.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            if (scenario == "policy-changed")
            {
                var office = await db.Offices.FindAsync(input.PickupOfficeId);
                office!.OperatingPolicy!.PreparationMinutes = 120;
            }
            await db.SaveChangesAsync();
            return true;
        });
        using var scope = Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReservationService>();
        if (scenario == "cached")
            (await service.CreateHoldAsync(id, session)).Should().NotBeNull();
        if (scenario is "wrong-session" or "unknown-version")
            (await service.CreateHoldAsync(id, scenario == "wrong-session" ? "other-session" : session)).Should().BeNull();
        else if (scenario is "valid" or "cached")
            (await service.CreateHoldAsync(id, session)).Should().NotBeNull();
        else
        {
            var hold = () => service.CreateHoldAsync(id, session);
            await hold.Should().ThrowAsync<ReservationQuoteConflictException>();
        }
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        saved.Status.Should().Be(scenario is "valid" or "cached" ? ReservationStatus.Hold : ReservationStatus.Draft);
        saved.PricingSnapshot!.BookingConditions!.DriverDeclaration.Should().BeNull();
    }

    [Theory]
    [InlineData("missing", false, HttpStatusCode.BadRequest)]
    [InlineData("missing", true, HttpStatusCode.BadRequest)]
    [InlineData("absent-driver", true, HttpStatusCode.BadRequest)]
    [InlineData("expired", true, HttpStatusCode.BadRequest)]
    [InlineData("return-day", true, HttpStatusCode.OK)]
    public async Task ExactBooking_RequiresDeclarationOfLicenceValidityThroughReturn(string expiryCase, bool unpaid, HttpStatusCode expected)
    {
        var input = ExactInput() with { ReturnDateTimeUtc = ExactInput().ReturnDateTimeUtc.Date.AddHours(22) };
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var returnDate = input.ReturnDateTimeUtc.Date.AddDays(1);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        request = request with
        {
            Driver = expiryCase == "absent-driver" ? null : request.Driver! with
            {
                Declaration = expiryCase == "missing" ? null : Declaration(30) with { LicenseValidThroughReturn = expiryCase != "expired" }
            }
        };
        using var response = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        var count = await WithDbContextAsync(db => db.Reservations.CountAsync(r => r.QuoteId == request.QuoteId));
        count.Should().Be(expected == HttpStatusCode.OK ? 1 : 0);
        if (expected != HttpStatusCode.OK)
        {
            using var retry = await SendReservationAsync(request with { Driver = new DriverInfoRequest { Declaration = Declaration(30) } },
                session, Guid.NewGuid().ToString(), unpaid);
            retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task ExactPayAtPickup_WorksWithLegacyUnpaidDisabled_WhileLegacyRemainsDisabled()
    {
        await WithDbContextAsync(async db =>
        {
            foreach (var name in new[] { PaymentMethodFeatureFlags.UnpaidRequest, PaymentMethodFeatureFlags.OnlinePayment })
            {
                var flag = await db.FeatureFlags.SingleOrDefaultAsync(f => f.Name == name);
                if (flag is null)
                    db.FeatureFlags.Add(new FeatureFlag { Name = name, Enabled = false, Description = "Local regression fixture" });
                else
                    flag.Enabled = false;
            }
            await db.SaveChangesAsync();
            return true;
        });
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        using var response = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid: true);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.SingleAsync(r => r.QuoteId == request.QuoteId));
        saved.Status.Should().Be(RentACar.Core.Enums.ReservationStatus.Confirmed);
        saved.UnpaidRequestExpiresAtUtc.Should().BeNull();
        (await WithDbContextAsync(db => db.PaymentIntents.CountAsync())).Should().Be(0);

        using var legacy = await SendReservationAsync(request with { VehicleId = null, QuoteId = null },
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), unpaid: true);
        legacy.StatusCode.Should().Be(HttpStatusCode.BadRequest, await legacy.Content.ReadAsStringAsync());
        (await legacy.Content.ReadAsStringAsync()).Should().Contain("aktif degil");
        (await WithDbContextAsync(db => db.Reservations.CountAsync(r => r.QuoteId == request.QuoteId))).Should().Be(1);
    }

    [Theory]
    [InlineData(true, null, HttpStatusCode.BadRequest)]
    [InlineData(false, null, HttpStatusCode.OK)]
    [InlineData(true, 18, HttpStatusCode.Conflict)]
    [InlineData(true, 21, HttpStatusCode.OK)]
    public async Task Quote_RequiresAgeOnlyForExactVehicle(bool exact, int? age, HttpStatusCode expected)
    {
        var input = ExactInput() with { VehicleId = exact ? TestDataSeeder.GroupOneId : null, DriverAge = age };
        using var response = await SendExactQuoteAsync(input, Guid.NewGuid().ToString());
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        if (expected == HttpStatusCode.BadRequest)
            (await response.Content.ReadAsStringAsync()).Should().Contain("Driver age is required");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task DatedCatalogue_QueryCountIsConstantAndPricesMatchQuotes(int fleetSize)
    {
        var input = ExactInput() with { ReturnOfficeId = TestDataSeeder.OfficeTwoId, DriverAge = 22 };
        var connectionString = await WithDbContextAsync(async db =>
        {
            var vehicles = await db.Vehicles.ToListAsync();
            var template = vehicles.Single(v => v.Id == input.VehicleId);
            foreach (var vehicle in vehicles) vehicle.Status = VehicleStatus.Maintenance;
            for (var i = 0; i < fleetSize; i++)
                db.Vehicles.Add(new Vehicle
                {
                    Plate = $"BATCH-{i}", Brand = "Synthetic", Model = "Catalogue", Year = 2026,
                    OfficeId = input.PickupOfficeId, GroupId = i % 2 == 0 ? null : template.GroupId,
                    RentalTerms = JsonSerializer.Deserialize<VehicleRentalTerms>(JsonSerializer.Serialize(template.RentalTerms))
                });
            await db.SaveChangesAsync();
            return db.Database.GetConnectionString()!;
        });
        var counter = new QueryCounter();
        await using var context = new RentACarDbContext(new DbContextOptionsBuilder<RentACarDbContext>()
            .UseNpgsql(connectionString).AddInterceptors(counter).Options);
        var service = new VehicleBookingService(context, new PricingService(context, new EfUnitOfWork(context)),
            new ReservationExtraPricingService(context));
        var offers = await service.GetAvailableAsync(input.PickupOfficeId, input.ReturnOfficeId,
            input.PickupDateTimeUtc, input.ReturnDateTimeUtc, input.DriverAge);
        offers.Should().HaveCount(fleetSize);
        counter.Count.Should().Be(2);
        foreach (var offer in offers)
        {
            var quoted = await service.CalculateAsync(input with
            {
                VehicleId = offer.Vehicle.Id, VehicleGroupId = offer.Vehicle.GroupId ?? Guid.Empty
            });
            offer.Pricing.Should().BeEquivalentTo(quoted.Pricing);
            offer.Pricing.AirportFee.Should().Be(250m);
            offer.Pricing.OneWayFee.Should().Be(500m);
            offer.Pricing.YoungDriverFee.Should().Be(200m);
        }
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(0, HttpStatusCode.OK)]
    public async Task AdminUpdate_ChecksShiftedPreparationInterval(int nextPickupOffset, HttpStatusCode expected)
    {
        var pickup = ExactInput().PickupDateTimeUtc;
        var oldReturn = pickup.AddDays(2);
        var newReturn = oldReturn.AddHours(1);
        var occupiedUntil = newReturn.AddHours(1);
        var id = await WithDbContextAsync(async db =>
        {
            var customer = new Customer { FullName = "Synthetic Update", Email = "update@rentacar.test", Phone = "+900000000000" };
            var reservation = new Reservation
            {
                PublicCode = "UPDATE-PREPARATION", Customer = customer, VehicleId = TestDataSeeder.GroupOneId,
                PickupOfficeId = TestDataSeeder.OfficeOneId, ReturnOfficeId = TestDataSeeder.OfficeOneId,
                PickupDateTime = pickup, ReturnDateTime = oldReturn, OccupiedUntilUtc = oldReturn.AddHours(1),
                Status = ReservationStatus.Confirmed, TotalAmount = 2000m
            };
            db.Reservations.Add(reservation);
            db.Reservations.Add(new Reservation
            {
                PublicCode = "NEXT-PREPARATION", Customer = customer, VehicleId = reservation.VehicleId,
                PickupOfficeId = reservation.PickupOfficeId, ReturnOfficeId = reservation.ReturnOfficeId,
                PickupDateTime = occupiedUntil.AddMinutes(nextPickupOffset), ReturnDateTime = occupiedUntil.AddDays(2),
                Status = ReservationStatus.Confirmed, TotalAmount = 2000m
            });
            await db.SaveChangesAsync();
            return reservation.Id;
        });
        await AuthenticateAsAdminAsync();
        using var response = await Client.PutAsJsonAsync($"/api/admin/v1/reservations/{id}",
            new UpdateReservationRequest { ReturnDateTimeUtc = newReturn });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        if (expected == HttpStatusCode.BadRequest) body.Should().Contain("overlapping reservations");
        var persisted = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        persisted.ReturnDateTime.Should().Be(expected == HttpStatusCode.OK ? newReturn : oldReturn);
        persisted.OccupiedUntilUtc.Should().Be(expected == HttpStatusCode.OK ? occupiedUntil : oldReturn.AddHours(1));
    }

    [Theory]
    [InlineData("missing-terms", false)]
    [InlineData("missing-rate", false)]
    [InlineData("underage", false)]
    [InlineData("missing-policy", false)]
    [InlineData("preparation-overlap", false)]
    [InlineData("preparation-boundary", true)]
    [InlineData("unknown-age", true)]
    public async Task DatedCatalogue_PreservesEligibilityAndPreparationRules(string scenario, bool included)
    {
        var input = ExactInput();
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == input.VehicleId);
            if (scenario == "missing-terms") vehicle.RentalTerms = null;
            if (scenario == "missing-rate") vehicle.RentalTerms!.Rates.Clear();
            if (scenario == "underage") vehicle.RentalTerms!.MinAge = 31;
            if (scenario == "missing-policy")
                (await db.Offices.SingleAsync(o => o.Id == input.PickupOfficeId)).OperatingPolicy = null;
            if (scenario.StartsWith("preparation-", StringComparison.Ordinal))
                db.Reservations.Add(new Reservation
                {
                    PublicCode = "CATALOGUE-PREPARATION", VehicleId = vehicle.Id,
                    Customer = new Customer { FullName = "Synthetic", Email = "catalogue@rentacar.test", Phone = "+900000000000" },
                    PickupOfficeId = input.PickupOfficeId, ReturnOfficeId = input.ReturnOfficeId,
                    PickupDateTime = input.ReturnDateTimeUtc.AddMinutes(scenario == "preparation-overlap" ? 59 : 60),
                    ReturnDateTime = input.ReturnDateTimeUtc.AddDays(2), Status = ReservationStatus.Confirmed
                });
            return await db.SaveChangesAsync();
        });
        var query = $"/api/v1/vehicles/available-exact?pickupOfficeId={input.PickupOfficeId}&returnOfficeId={input.ReturnOfficeId}" +
            $"&pickupDateTimeUtc={Uri.EscapeDataString(input.PickupDateTimeUtc.ToString("O"))}&returnDateTimeUtc={Uri.EscapeDataString(input.ReturnDateTimeUtc.ToString("O"))}" +
            (scenario == "unknown-age" ? "" : "&driverAge=30");
        using var response = await Client.GetAsync(query);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").EnumerateArray()
            .Any(item => item.GetProperty("vehicle").GetProperty("id").GetGuid() == input.VehicleId).Should().Be(included);
    }

    [Theory]
    [InlineData(false, "tr", "tr-TR")]
    [InlineData(true, "tr", "tr-TR")]
    [InlineData(true, "en", "en-US")]
    [InlineData(true, "de", "de-DE")]
    [InlineData(true, "ar", "ar-SA")]
    [InlineData(true, "ru", "ru-RU")]
    public async Task UnpaidConfirmation_QueuesNotificationsOnceOnlyForExactBooking(bool exact, string locale, string notificationLocale)
    {
        var input = ExactInput() with { VehicleId = exact ? TestDataSeeder.GroupOneId : null, Locale = locale };
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        using var created = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), true);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        using var replay = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), true);
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        var jobs = await WithDbContextAsync(db => db.BackgroundJobs.AsNoTracking()
            .Where(j => j.Type == BackgroundJobTypes.NotificationEmailSend || j.Type == BackgroundJobTypes.NotificationSmsSend)
            .ToListAsync());
        jobs.Should().HaveCount(exact ? 6 : 0);
        if (exact)
        {
            jobs.Count(j => j.Type == BackgroundJobTypes.NotificationEmailSend).Should().Be(3);
            jobs.Count(j => j.Type == BackgroundJobTypes.NotificationSmsSend).Should().Be(3);
            jobs.Count(j => j.ScheduledAt == input.PickupDateTimeUtc.AddHours(-24)).Should().Be(2);
            jobs.Count(j => j.ScheduledAt == input.ReturnDateTimeUtc.AddHours(-24)).Should().Be(2);
            jobs.Select(j => JsonDocument.Parse(j.Payload).RootElement.GetProperty("TemplateKey").GetString())
                .Distinct().Should().HaveCount(3);
            jobs.Select(j => JsonDocument.Parse(j.Payload).RootElement.GetProperty("Locale").GetString())
                .Should().OnlyContain(value => value == notificationLocale);
        }
    }

    [Fact]
    public async Task ExactConfirmation_NotificationQueueFailureRollsBackAndAllowsRetry()
    {
        await WithDbContextAsync(db => db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE background_jobs ADD CONSTRAINT test_reject_sms CHECK (type <> 'notification-sms-send')"));
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        using var rejected = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), true);
        rejected.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(0);
        (await WithDbContextAsync(db => db.BackgroundJobs.CountAsync(j =>
            j.Type == BackgroundJobTypes.NotificationEmailSend || j.Type == BackgroundJobTypes.NotificationSmsSend)))
            .Should().Be(0);
        await WithDbContextAsync(db => db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE background_jobs DROP CONSTRAINT test_reject_sms"));
        using var retried = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), true);
        retried.StatusCode.Should().Be(HttpStatusCode.OK, await retried.Content.ReadAsStringAsync());
        (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(1);
        (await WithDbContextAsync(db => db.BackgroundJobs.CountAsync(j =>
            j.Type == BackgroundJobTypes.NotificationEmailSend || j.Type == BackgroundJobTypes.NotificationSmsSend)))
            .Should().Be(6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactReservation_AdminCannotReplaceOrRemovePromisedVehicle(bool unpaid)
    {
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        using var created = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var original = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        var targetId = await WithDbContextAsync(async db =>
        {
            var target = new Vehicle { Plate = "EXACT-NO-SUBSTITUTE", Brand = "Synthetic", Model = "Alternative",
                Year = 2026, OfficeId = input.PickupOfficeId, GroupId = input.VehicleGroupId };
            db.Vehicles.Add(target);
            await db.SaveChangesAsync();
            return target.Id;
        });
        await AuthenticateAsAdminAsync();
        using var reassigned = await Client.PostAsJsonAsync($"/api/admin/v1/reservations/{original.Id}/assign-vehicle", targetId);
        reassigned.StatusCode.Should().Be(HttpStatusCode.Conflict, await reassigned.Content.ReadAsStringAsync());
        using var removed = await Client.PostAsync($"/api/admin/v1/reservations/{original.Id}/unassign-vehicle", null);
        removed.StatusCode.Should().Be(HttpStatusCode.Conflict, await removed.Content.ReadAsStringAsync());
        using var unchanged = await Client.PostAsJsonAsync($"/api/admin/v1/reservations/{original.Id}/assign-vehicle", input.VehicleId);
        unchanged.StatusCode.Should().Be(HttpStatusCode.OK, await unchanged.Content.ReadAsStringAsync());
        var persisted = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == original.Id));
        persisted.VehicleId.Should().Be(input.VehicleId!.Value);
        persisted.UpdatedAt.Should().Be(original.UpdatedAt);
        persisted.PricingSnapshot.Should().BeEquivalentTo(original.PricingSnapshot);
        persisted.QuoteReplayProof.Should().BeEquivalentTo(original.QuoteReplayProof);
        persisted.QuoteId.Should().Be(original.QuoteId);
        Client.DefaultRequestHeaders.Authorization = null;
        using var replay = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        replayJson.RootElement.GetProperty("data").GetProperty("vehicleId").GetGuid().Should().Be(input.VehicleId.Value);
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }

    private static CreateReservationQuoteRequest ExactInput() => new()
    {
        VehicleId = TestDataSeeder.GroupOneId, VehicleGroupId = TestDataSeeder.GroupOneId,
        PickupOfficeId = TestDataSeeder.OfficeOneId, ReturnOfficeId = TestDataSeeder.OfficeOneId,
        PickupDateTimeUtc = DateTime.UtcNow.Date.AddDays(10).AddHours(10),
        ReturnDateTimeUtc = DateTime.UtcNow.Date.AddDays(13).AddHours(10), DriverAge = 30
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingExactDrafts_SecondHoldReturnsVehicleUnavailable(bool groupless)
    {
        if (groupless)
        {
            await WithDbContextAsync(async db =>
            {
                var vehicle = await db.Vehicles.FindAsync(TestDataSeeder.GroupOneId);
                vehicle!.GroupId = null;
                return await db.SaveChangesAsync();
            });
        }
        var input = ExactInput() with { VehicleGroupId = groupless ? Guid.Empty : TestDataSeeder.GroupOneId };
        var sessions = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        var ids = new List<Guid>();
        foreach (var session in sessions)
        {
            using var quote = await SendExactQuoteAsync(input, session);
            quote.StatusCode.Should().Be(HttpStatusCode.OK);
            using var draft = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quote)), session, Guid.NewGuid().ToString());
            draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
            ids.Add(json.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        }
        for (var i = 0; i < ids.Count; i++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{ids[i]}/hold");
            message.Headers.Add("X-Session-Id", sessions[i]);
            using var hold = await Client.SendAsync(message);
            var body = await hold.Content.ReadAsStringAsync();
            hold.StatusCode.Should().Be(i == 0 ? HttpStatusCode.OK : HttpStatusCode.Conflict, body);
            if (i == 1) body.Should().Contain("Selected vehicle is unavailable");
        }
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().Where(r => ids.Contains(r.Id)).ToListAsync());
        saved.Should().ContainSingle(r => r.Status == ReservationStatus.Hold && r.Id == ids[0]);
        saved.Should().ContainSingle(r => r.Status == ReservationStatus.Draft && r.Id == ids[1]);
        saved.Should().OnlyContain(r => r.VehicleId == input.VehicleId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactQuoteReplay_RejectsChangedCheckoutOperation(bool unpaidFirst)
    {
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        var key = Guid.NewGuid().ToString();
        using var created = await SendReservationAsync(request, session, key, unpaidFirst);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());

        foreach (var retryKey in new[] { key, Guid.NewGuid().ToString() })
        {
            using var wrongOperation = await SendReservationAsync(request, session, retryKey, !unpaidFirst);
            wrongOperation.StatusCode.Should().Be(HttpStatusCode.Conflict, await wrongOperation.Content.ReadAsStringAsync());
            using var sameOperation = await SendReservationAsync(request, session, retryKey, unpaidFirst);
            sameOperation.StatusCode.Should().Be(HttpStatusCode.OK, await sameOperation.Content.ReadAsStringAsync());
        }

        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.QuoteId == request.QuoteId));
        saved.Status.Should().Be(unpaidFirst ? ReservationStatus.Confirmed : ReservationStatus.Draft);
        saved.QuoteReplayProof!.CheckoutOperation.Should().Be(unpaidFirst ? "unpaid" : "draft");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExactReplay_RejectsChangedPersonalDetailsWithWarmAndExpiredQuote(bool unpaid, bool expired)
    {
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = ExactReservation(input, await QuoteIdAsync(quote));
        using var created = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        if (expired)
        {
            var redis = Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            (await redis.KeyDeleteAsync($"reservation_quote:{request.QuoteId:N}")).Should().BeTrue();
            (await Services.GetRequiredService<IReservationQuoteStore>().GetAsync(request.QuoteId!.Value)).Should().BeNull();
        }
        CreateReservationRequest[] changes =
        [
            request with { Customer = request.Customer with { Email = "changed@example.test" } },
            request with { Driver = request.Driver! with { Declaration = request.Driver!.Declaration! with { LicenseYearsAtPickup = 6 } } }
        ];
        foreach (var changed in changes)
        {
            using var rejected = await SendReservationAsync(changed, session, Guid.NewGuid().ToString(), unpaid);
            rejected.StatusCode.Should().Be(HttpStatusCode.Conflict, await rejected.Content.ReadAsStringAsync());
        }
        using var replay = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.Include(r => r.Customer).SingleAsync());
        saved.Customer!.FullName.Should().Be($"{request.Customer.FirstName} {request.Customer.LastName}");
        saved.DriverLicenseExpiryDate.Should().BeNull();
        saved.DriverDateOfBirth.Should().BeNull();
        saved.DriverLicenseNumber.Should().BeNull();
        saved.Notes.Should().Be(request.Notes);
    }

    [Fact]
    public async Task ExactQuote_ExtrasOrderDoesNotChangeFingerprintOrInvalidateHold()
    {
        var extras = await WithDbContextAsync(async db =>
        {
            var options = await db.ReservationExtraOptions.OrderBy(e => e.Id).Take(2).ToListAsync();
            options.Should().HaveCount(2);
            foreach (var option in options)
            {
                option.IsActive = true;
                option.IsArchived = false;
            }
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == TestDataSeeder.GroupOneId);
            vehicle.RentalTerms!.ExtraOptionIds = options.Select(e => e.Id).ToArray();
            await db.SaveChangesAsync();
            return options.Select(e => new SelectedReservationExtraInput { OptionId = e.Id, OptionVersion = e.Version, Quantity = 1 }).ToArray();
        });
        var input = ExactInput() with { SelectedExtras = extras };
        var session = Guid.NewGuid().ToString();
        using var first = await SendExactQuoteAsync(input, session);
        using var second = await SendExactQuoteAsync(input with { SelectedExtras = extras.Reverse().ToArray() }, session);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.Should().Be(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        var store = Services.GetRequiredService<IReservationQuoteStore>();
        var firstQuote = await store.GetAsync(await QuoteIdAsync(first));
        var secondQuote = await store.GetAsync(await QuoteIdAsync(second));
        firstQuote!.PricingSnapshot.BookingConditions!.PolicyFingerprint.Should().Be(secondQuote!.PricingSnapshot.BookingConditions!.PolicyFingerprint);
        using var draft = await SendReservationAsync(ExactReservation(input, firstQuote.QuoteId), session, Guid.NewGuid().ToString());
        draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reservation = await db.Reservations.Include(r => r.Vehicle).Include(r => r.SelectedExtras).SingleAsync();
        reservation.SelectedExtras = reservation.SelectedExtras.OrderByDescending(e => e.ExtraOptionId).ToList();
        await scope.ServiceProvider.GetRequiredService<VehicleBookingService>().ValidateDraftForHoldAsync(reservation, CancellationToken.None);
        await transaction.RollbackAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentExactHolds_SerializeOneVehicleWithoutBlockingAnother(bool sameVehicle)
    {
        var secondId = sameVehicle ? TestDataSeeder.GroupOneId : await WithDbContextAsync(async db =>
        {
            var original = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == TestDataSeeder.GroupOneId);
            var other = new Vehicle
            {
                Plate = "SYNTHETIC-PARALLEL", Brand = original.Brand, Model = original.Model,
                Year = original.Year, GroupId = original.GroupId, OfficeId = original.OfficeId,
                RentalTerms = original.RentalTerms
            };
            db.Vehicles.Add(other);
            await db.SaveChangesAsync();
            return other.Id;
        });
        var requests = new List<(Guid Id, string Session)>();
        foreach (var vehicleId in new[] { TestDataSeeder.GroupOneId, secondId })
        {
            var input = ExactInput() with { VehicleId = vehicleId };
            var session = Guid.NewGuid().ToString();
            using var quote = await SendExactQuoteAsync(input, session);
            quote.StatusCode.Should().Be(HttpStatusCode.OK, await quote.Content.ReadAsStringAsync());
            using var draft = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quote)), session, Guid.NewGuid().ToString());
            draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
            requests.Add((json.RootElement.GetProperty("data").GetProperty("id").GetGuid(), session));
        }
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{request.Id}/hold");
            message.Headers.Add("X-Session-Id", request.Session);
            using var response = await Client.SendAsync(message);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        }));
        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(sameVehicle ? 1 : 2,
            string.Join("; ", results.Select(r => r.Body)));
        if (sameVehicle)
        {
            var loser = results.Single(r => r.StatusCode != HttpStatusCode.OK);
            loser.StatusCode.Should().Be(HttpStatusCode.Conflict, loser.Body);
            loser.Body.Should().Contain("Selected vehicle is unavailable");
        }
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().ToListAsync());
        saved.Count(r => r.Status == ReservationStatus.Hold).Should().Be(sameVehicle ? 1 : 2);
        saved.Select(r => r.VehicleId).Should().BeEquivalentTo(new[] { TestDataSeeder.GroupOneId, secondId });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactHold_ContentionPreservesOwnerAndAllowsRetry(bool releaseDuringWait)
    {
        var input = ExactInput();
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        using var draft = await SendReservationAsync(ExactReservation(input, await QuoteIdAsync(quote)), session, Guid.NewGuid().ToString());
        draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var redis = Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var key = $"hold:exact:{input.VehicleId:N}";
        var owner = Guid.NewGuid().ToString();
        (await redis.LockTakeAsync(key, owner, TimeSpan.FromSeconds(30))).Should().BeTrue();
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{id}/hold");
            message.Headers.Add("X-Session-Id", session);
            var pending = Client.SendAsync(message);
            if (releaseDuringWait)
            {
                await Task.Delay(250);
                pending.IsCompleted.Should().BeFalse();
                (await redis.LockReleaseAsync(key, owner)).Should().BeTrue();
            }
            using var response = await pending;
            response.StatusCode.Should().Be(releaseDuringWait ? HttpStatusCode.OK : HttpStatusCode.Conflict,
                await response.Content.ReadAsStringAsync());
            if (!releaseDuringWait)
            {
                (await response.Content.ReadAsStringAsync()).Should().Contain("already being used");
                (await redis.StringGetAsync(key)).ToString().Should().Be(owner);
                (await redis.LockReleaseAsync(key, owner)).Should().BeTrue();
                using var retry = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{id}/hold");
                retry.Headers.Add("X-Session-Id", session);
                using var retried = await Client.SendAsync(retry);
                retried.StatusCode.Should().Be(HttpStatusCode.OK, await retried.Content.ReadAsStringAsync());
            }
        }
        finally
        {
            await redis.LockReleaseAsync(key, owner);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("deposit")]
    [InlineData("rate")]
    [InlineData("allowed-extra")]
    [InlineData("pickup-window")]
    [InlineData("return-window")]
    [InlineData("closed-date")]
    public async Task ExactHold_PolicyCollectionOrderIsIgnoredButContentChangesAreRejected(string change)
    {
        var input = ExactInput() with { ReturnOfficeId = TestDataSeeder.OfficeTwoId };
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == input.VehicleId);
            vehicle.RentalTerms!.Rates.Add(new VehicleRentalRate
            {
                StartDate = DateOnly.FromDateTime(input.PickupDateTimeUtc.AddYears(-2)),
                EndDate = DateOnly.FromDateTime(input.PickupDateTimeUtc.AddYears(-1)), DailyPrice = 1m
            });
            vehicle.RentalTerms.ExtraOptionIds.Length.Should().BeGreaterThan(1);
            foreach (var office in await db.Offices.Where(o => o.Id == input.PickupOfficeId || o.Id == input.ReturnOfficeId).ToListAsync())
            {
                var policy = office.OperatingPolicy!;
                policy.PickupWindows = Enum.GetValues<DayOfWeek>().SelectMany(day => new[]
                {
                    new OfficeOperatingWindow { Day = day, StartMinute = 0, EndMinute = 720 },
                    new OfficeOperatingWindow { Day = day, StartMinute = 720, EndMinute = 1440 }
                }).ToList();
                policy.ReturnWindows = policy.PickupWindows.Select(w => new OfficeOperatingWindow
                    { Day = w.Day, StartMinute = w.StartMinute, EndMinute = w.EndMinute }).ToList();
                policy.ClosedDates = [DateOnly.FromDateTime(input.PickupDateTimeUtc.AddYears(1)),
                    DateOnly.FromDateTime(input.PickupDateTimeUtc.AddYears(1).AddDays(1))];
            }
            return await db.SaveChangesAsync();
        });
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK, await quote.Content.ReadAsStringAsync());
        var quoteId = await QuoteIdAsync(quote);
        using var draft = await SendReservationAsync(ExactReservation(input, quoteId), session, Guid.NewGuid().ToString());
        draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == input.VehicleId);
            vehicle.RentalTerms!.Rates.Reverse();
            vehicle.RentalTerms.ExtraOptionIds = vehicle.RentalTerms.ExtraOptionIds.Reverse().ToArray();
            foreach (var office in await db.Offices.Where(o => o.Id == input.PickupOfficeId || o.Id == input.ReturnOfficeId).ToListAsync())
            {
                office.OperatingPolicy!.PickupWindows.Reverse();
                office.OperatingPolicy.ReturnWindows.Reverse();
                office.OperatingPolicy.ClosedDates = office.OperatingPolicy.ClosedDates.Reverse().ToArray();
            }
            return await db.SaveChangesAsync();
        });
        using var reordered = await SendExactQuoteAsync(input, session);
        reordered.StatusCode.Should().Be(HttpStatusCode.OK, await reordered.Content.ReadAsStringAsync());
        var store = Services.GetRequiredService<IReservationQuoteStore>();
        var accepted = await store.GetAsync(quoteId);
        var equivalent = await store.GetAsync(await QuoteIdAsync(reordered));
        equivalent!.PricingSnapshot.BookingConditions!.PolicyFingerprint.Should().Be(accepted!.PricingSnapshot.BookingConditions!.PolicyFingerprint);
        if (change != "none")
        {
            await WithDbContextAsync(async db =>
            {
                var vehicle = await db.Vehicles.SingleAsync(v => v.Id == input.VehicleId);
                var pickup = await db.Offices.SingleAsync(o => o.Id == input.PickupOfficeId);
                var dropoff = await db.Offices.SingleAsync(o => o.Id == input.ReturnOfficeId);
                switch (change)
                {
                    case "deposit": vehicle.RentalTerms!.DepositAmount += 1m; break;
                    case "rate": vehicle.RentalTerms!.Rates.ForEach(rate => rate.DailyPrice += 1m); break;
                    case "allowed-extra": vehicle.RentalTerms!.ExtraOptionIds = vehicle.RentalTerms.ExtraOptionIds.Skip(1).ToArray(); break;
                    case "pickup-window": pickup.OperatingPolicy!.PickupWindows.First(w => w.StartMinute == 0).StartMinute = 1; break;
                    case "return-window": dropoff.OperatingPolicy!.ReturnWindows.First(w => w.StartMinute == 0).StartMinute = 1; break;
                    case "closed-date": dropoff.OperatingPolicy!.ClosedDates = [DateOnly.FromDateTime(input.ReturnDateTimeUtc)]; break;
                }
                return await db.SaveChangesAsync();
            });
        }
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{id}/hold");
        message.Headers.Add("X-Session-Id", session);
        using var hold = await Client.SendAsync(message);
        hold.StatusCode.Should().Be(change == "none" ? HttpStatusCode.OK : HttpStatusCode.Conflict, await hold.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        saved.Status.Should().Be(change == "none" ? ReservationStatus.Hold : ReservationStatus.Draft);
    }

    [Theory]
    [InlineData(20, 29, false)]
    [InlineData(21, 30, false)]
    [InlineData(22, 30, false)]
    [InlineData(23, 30, false)]
    [InlineData(21, 30, true)]
    [InlineData(21, 29, false)]
    public async Task ExactBooking_ValidatesReconfirmedAgeAcrossTurkeyPickupBoundary(int utcHour, int quotedAge, bool unpaid)
    {
        var birthday = DateTime.UtcNow.Date.AddDays(11);
        var pickup = birthday.AddDays(-1).AddHours(utcHour);
        var input = ExactInput() with { PickupDateTimeUtc = pickup, ReturnDateTimeUtc = pickup.AddDays(3), DriverAge = quotedAge };
        var session = Guid.NewGuid().ToString();
        using var quote = await SendExactQuoteAsync(input, session);
        quote.StatusCode.Should().Be(HttpStatusCode.OK, await quote.Content.ReadAsStringAsync());
        var original = ExactReservation(input, await QuoteIdAsync(quote));
        var request = original with { Driver = new DriverInfoRequest { Declaration = Declaration(utcHour >= 21 ? 30 : 29) } };
        var accepted = quotedAge == (utcHour >= 21 ? 30 : 29);
        using var created = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        created.StatusCode.Should().Be(accepted ? HttpStatusCode.OK : HttpStatusCode.Conflict, await created.Content.ReadAsStringAsync());
        if (!accepted)
        {
            (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(0);
            return;
        }
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        if (!unpaid)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{id}/hold");
            message.Headers.Add("X-Session-Id", session);
            using var hold = await Client.SendAsync(message);
            hold.StatusCode.Should().Be(HttpStatusCode.OK, await hold.Content.ReadAsStringAsync());
        }
        using var replay = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(1);
    }

    private static DriverDeclaration Declaration(int age) => new()
    {
        AgeAtPickup = age, LicenseYearsAtPickup = 5,
        LicenseValidThroughReturn = true, DocumentsAvailableAtPickup = true
    };

    private static CreateReservationRequest ExactReservation(CreateReservationQuoteRequest input, Guid quoteId) => new()
    {
        VehicleId = input.VehicleId, VehicleGroupId = input.VehicleGroupId,
        PickupOfficeId = input.PickupOfficeId, ReturnOfficeId = input.ReturnOfficeId,
        PickupDateTimeUtc = input.PickupDateTimeUtc, ReturnDateTimeUtc = input.ReturnDateTimeUtc,
        QuoteId = quoteId, DriverAge = input.DriverAge, Locale = input.Locale,
        Driver = new DriverInfoRequest { Declaration = Declaration(input.DriverAge ?? 30) },
        Customer = new CustomerInfoRequest
        {
            FirstName = "Synthetic", LastName = "Policy", Phone = "+900000000000",
            Email = $"policy-{Guid.NewGuid():N}@example.test", DateOfBirth = input.PickupDateTimeUtc.AddYears(-30),
            DriverLicenseIssueDate = input.PickupDateTimeUtc.AddYears(-5)
        }
    };

    private async Task<HttpResponseMessage> SendExactQuoteAsync(CreateReservationQuoteRequest input, string session)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing/quote") { Content = JsonContent.Create(input) };
        message.Headers.Add("X-Session-Id", session);
        return await Client.SendAsync(message);
    }

    private static async Task<Guid> QuoteIdAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("quoteId").GetGuid();
    }

    private async Task<HttpResponseMessage> SendReservationAsync(
        CreateReservationRequest request,
        string sessionId,
        string idempotencyKey,
        bool unpaid = false)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, unpaid ? "/api/v1/reservations/unpaid-requests" : "/api/v1/reservations")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Session-Id", sessionId);
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await Client.SendAsync(message);
    }
}
