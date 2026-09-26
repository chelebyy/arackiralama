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
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
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
            Driver = new DriverInfoRequest { LicenseExpiryDate = pickup.AddYears(2) },
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
        persisted.QuoteReplayProof!.SchemaVersion.Should().Be(exact ? 2 : 1);
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
                Driver = new DriverInfoRequest { LicenseExpiryDate = pickup.AddYears(2) },
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrouplessDraft_HoldRevalidatesAcceptedPolicy(bool changed)
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
        using var holdRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{id}/hold");
        holdRequest.Headers.Add("X-Session-Id", session);
        using var hold = await Client.SendAsync(holdRequest);
        hold.StatusCode.Should().Be(changed ? HttpStatusCode.Conflict : HttpStatusCode.OK, await hold.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        saved.Status.Should().Be(changed ? RentACar.Core.Enums.ReservationStatus.Draft : RentACar.Core.Enums.ReservationStatus.Hold);
    }

    [Theory]
    [InlineData("missing", false, HttpStatusCode.BadRequest)]
    [InlineData("missing", true, HttpStatusCode.BadRequest)]
    [InlineData("absent-driver", true, HttpStatusCode.BadRequest)]
    [InlineData("expired", true, HttpStatusCode.Conflict)]
    [InlineData("return-day", true, HttpStatusCode.OK)]
    public async Task ExactBooking_RequiresLicenseExpiryThroughTurkeyReturnDate(string expiryCase, bool unpaid, HttpStatusCode expected)
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
                LicenseExpiryDate = expiryCase switch
                {
                    "missing" => null,
                    "expired" => returnDate.AddDays(-1),
                    _ => returnDate
                }
            }
        };
        using var response = await SendReservationAsync(request, session, Guid.NewGuid().ToString(), unpaid);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        var count = await WithDbContextAsync(db => db.Reservations.CountAsync(r => r.QuoteId == request.QuoteId));
        count.Should().Be(expected == HttpStatusCode.OK ? 1 : 0);
        if (expected != HttpStatusCode.OK)
        {
            using var retry = await SendReservationAsync(request with { Driver = new DriverInfoRequest { LicenseExpiryDate = returnDate } },
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

    private static CreateReservationRequest ExactReservation(CreateReservationQuoteRequest input, Guid quoteId) => new()
    {
        VehicleId = input.VehicleId, VehicleGroupId = input.VehicleGroupId,
        PickupOfficeId = input.PickupOfficeId, ReturnOfficeId = input.ReturnOfficeId,
        PickupDateTimeUtc = input.PickupDateTimeUtc, ReturnDateTimeUtc = input.ReturnDateTimeUtc,
        QuoteId = quoteId, DriverAge = input.DriverAge,
        Driver = new DriverInfoRequest { LicenseExpiryDate = input.ReturnDateTimeUtc.AddYears(2) },
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
