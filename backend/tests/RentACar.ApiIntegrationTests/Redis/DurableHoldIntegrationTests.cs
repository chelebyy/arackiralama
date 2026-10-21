using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Constants;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services;
using StackExchange.Redis;
using RentACar.Worker;
using Xunit;

namespace RentACar.ApiIntegrationTests.Redis;

public sealed class DurableHoldIntegrationTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task RedisTtlExpiry_WorkerRestart_ReleasesPostgresOverlapConstraint()
    {
        var reservation = await SeedAsync();
        using (var scope = Services.CreateScope())
        {
            var holds = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
            await holds.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMilliseconds(300));
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (await redis.KeyExistsAsync($"hold:{reservation.Id}"))
                await Task.Delay(20, timeout.Token);
        }

        await RunWorkerAsync();

        await WithDbContextAsync(async db =>
        {
            (await db.Reservations.FindAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Expired);
            (await db.ReservationHolds.AnyAsync(h => h.ReservationId == reservation.Id)).Should().BeFalse();
            var replacement = NewReservation(reservation.CustomerId, reservation.VehicleId, reservation.PickupOfficeId);
            db.Reservations.Add(replacement);
            await db.SaveChangesAsync();
            return true;
        });
    }

    [Fact]
    public async Task ApiCreateHold_CommitsStatusAndExpiryTogether_AndGuestCanReleaseAndRecreate()
    {
        var reservation = await SeedAsync(ReservationStatus.Draft);
        using var scope = Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var holds = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();

        var created = await reservations.CreateHoldAsync(reservation.Id, "guest");
        created.Should().NotBeNull();
        created!.ExpiresAt.Should().Be((await holds.GetHoldAsync(reservation.Id))!.ExpiresAt);
        await reservations.ReleaseHoldByReservationIdAsync(reservation.Id);
        (await reservations.CreateHoldAsync(reservation.Id, "guest")).Should().NotBeNull();
        (await holds.IsHoldValidAsync(reservation.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task OuterTransactionRollback_DoesNotLeaveDurableHoldOrAuthorizeRedisGhost()
    {
        var reservation = await SeedAsync(ReservationStatus.Draft);
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var current = await db.Reservations.FindAsync(reservation.Id);
            current!.Status = ReservationStatus.Hold;
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IReservationHoldService>()
                .CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(15));
            await transaction.RollbackAsync();
        }
        using var readScope = Services.CreateScope();
        var holds = readScope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        (await holds.GetHoldAsync(reservation.Id)).Should().BeNull();
        (await holds.IsVehicleHeldAsync(reservation.VehicleId)).Should().BeFalse();
        await WithDbContextAsync(async db =>
        {
            (await db.Reservations.FindAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Draft);
            (await db.ReservationHolds.AnyAsync()).Should().BeFalse();
            return true;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedExpiry_AfterRecreationOrExtension_PreservesCurrentHold(bool extend)
    {
        var reservation = await SeedAsync();
        using (var scope = Services.CreateScope())
        {
            var holds = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
            await holds.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(5));
            var db = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
            db.BackgroundJobs.Add(new BackgroundJob
            {
                Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
                Payload = JsonSerializer.Serialize(new { ReservationId = reservation.Id }),
                ScheduledAt = DateTime.UtcNow,
                Status = BackgroundJobStatus.Pending
            });
            await db.SaveChangesAsync();
            if (extend)
                (await holds.ExtendHoldAsync(reservation.Id, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))).Should().BeTrue();
            else
                await holds.CreateHoldAsync(reservation.Id, reservation.VehicleId, "new-guest", TimeSpan.FromMinutes(15));
        }

        await RunWorkerAsync();

        await WithDbContextAsync(async db =>
        {
            (await db.Reservations.FindAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Hold);
            (await db.ReservationHolds.CountAsync()).Should().Be(1);
            (await db.BackgroundJobs.SingleAsync(j => j.Type == BackgroundJobTypes.ReservationHoldReleaseExpired)).Status
                .Should().Be(BackgroundJobStatus.Completed);
            return true;
        });
    }

    [Fact]
    public async Task Expiry_WaitsForConcurrentExtension_ThenReadsCommittedExpiry()
    {
        var reservation = await SeedAsync();
        using var extensionScope = Services.CreateScope();
        var extensionDb = extensionScope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var extensionService = extensionScope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        await extensionService.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(1));
        await using var transaction = await extensionDb.Database.BeginTransactionAsync();
        await extensionService.ExtendHoldAsync(reservation.Id, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        using var expiryScope = Services.CreateScope();
        var expiryDb = expiryScope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        await expiryDb.Database.OpenConnectionAsync();
        var processId = ((NpgsqlConnection)expiryDb.Database.GetDbConnection()).ProcessID;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var expiry = expiryScope.ServiceProvider.GetRequiredService<IReservationHoldService>()
            .ExpireHoldAsync(reservation.Id, timeout.Token);
        await WaitForRowLockAsync(processId, timeout.Token);
        await transaction.CommitAsync();
        (await expiry).Should().BeTrue();
        (await expiryDb.Reservations.FindAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Hold);
        (await expiryDb.ReservationHolds.SingleAsync()).ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(4));
    }

    [Fact]
    public async Task RedisLossAndLateVehicleKey_ReleasesDatabaseWithoutDeletingOtherReservationKey()
    {
        var reservation = await SeedAsync();
        using var scope = Services.CreateScope();
        var holds = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        await holds.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(15));
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        await redis.KeyDeleteAsync($"hold:{reservation.Id}");
        var other = Guid.NewGuid().ToString();
        await redis.StringSetAsync($"vehicle_hold:{reservation.VehicleId}", other, TimeSpan.FromMinutes(15));
        var db = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        await db.ReservationHolds.Where(h => h.ReservationId == reservation.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));

        await RunWorkerAsync();

        (await redis.StringGetAsync($"vehicle_hold:{reservation.VehicleId}")).ToString().Should().Be(other);
        await db.Entry(reservation).ReloadAsync();
        reservation.Status.Should().Be(ReservationStatus.Expired);
    }

    [Fact]
    public async Task WorkerRestart_AfterClaimBeforeExpiry_ReclaimsAbandonedJob()
    {
        var reservation = await SeedAsync();
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IReservationHoldService>()
                .CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(1));
            var db = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
            await db.ReservationHolds.ExecuteUpdateAsync(s => s.SetProperty(h => h.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
            db.BackgroundJobs.Add(new BackgroundJob
            {
                Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
                Payload = JsonSerializer.Serialize(new { ReservationId = reservation.Id }),
                Status = BackgroundJobStatus.Processing,
                ScheduledAt = DateTime.UtcNow.AddMinutes(-10), UpdatedAt = DateTime.UtcNow.AddMinutes(-6)
            });
            await db.SaveChangesAsync();
        }

        await RunWorkerAsync();

        await WithDbContextAsync(async db =>
        {
            (await db.Reservations.FindAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Expired);
            (await db.BackgroundJobs.SingleAsync(j => j.Type == BackgroundJobTypes.ReservationHoldReleaseExpired)).Status
                .Should().Be(BackgroundJobStatus.Completed);
            return true;
        });
    }

    private async Task WaitForRowLockAsync(int processId, CancellationToken cancellationToken)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        while (!await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {processId} AND wait_event_type = 'Lock') AS \"Value\"")
            .SingleAsync(cancellationToken))
            await Task.Delay(20, cancellationToken);
    }

    private async Task RunWorkerAsync()
    {
        using var worker = new RentACar.Worker.Worker(Services, Options.Create(new DailyBackupOptions { Enabled = false }),
            NullLogger<RentACar.Worker.Worker>.Instance);
        foreach (var method in new[] { "EnqueueExpiredHoldJobsAsync", "ProcessExpiredHoldJobsAsync" })
            await (Task)typeof(RentACar.Worker.Worker).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(worker, [CancellationToken.None])!;
    }

    private Task<Reservation> SeedAsync(ReservationStatus status = ReservationStatus.Hold) => WithDbContextAsync(async db =>
    {
        var vehicle = await db.Vehicles.FirstAsync();
        var customer = new Customer
        {
            FullName = "Guest hold test",
            Phone = "+90 555 111 22 33",
            Email = $"hold-{Guid.NewGuid():N}@example.com",
            IdentityNumber = "12345678901",
            Nationality = "TR"
        };
        db.Customers.Add(customer);
        var reservation = NewReservation(customer.Id, vehicle.Id, vehicle.OfficeId);
        reservation.Status = status;
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation;
    });

    private static Reservation NewReservation(Guid customerId, Guid vehicleId, Guid officeId) => new()
    {
        PublicCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
        CustomerId = customerId,
        VehicleId = vehicleId,
        PickupOfficeId = officeId,
        ReturnOfficeId = officeId,
        PickupDateTime = DateTime.UtcNow.Date.AddDays(30),
        ReturnDateTime = DateTime.UtcNow.Date.AddDays(32),
        Status = ReservationStatus.Hold,
        TotalAmount = 1000m
    };
}
