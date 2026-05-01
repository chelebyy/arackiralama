using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using StackExchange.Redis;
using System.Diagnostics;
using FleetContracts = RentACar.API.Contracts.Fleet;
using RentACar.API.Contracts.Payments;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ReservationServiceTests
{
    private readonly Mock<IReservationRepository> _reservationRepositoryMock;
    private readonly Mock<IRepository<Customer>> _customerRepositoryMock;
    private readonly Mock<IRepository<Vehicle>> _vehicleRepositoryMock;
    private readonly Mock<IVehicleRepository> _vehicleRepositorySpecificMock;
    private readonly Mock<IRepository<Office>> _officeRepositoryMock;
    private readonly Mock<IReservationHoldService> _holdServiceMock;
    private readonly Mock<IApplicationDbContext> _applicationDbContextMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDatabaseMock;
    private readonly Mock<IFleetService> _fleetServiceMock;
    private readonly Mock<IPricingService> _pricingServiceMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly Mock<INotificationQueueService> _notificationQueueServiceMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<ReservationService>> _loggerMock;
    private readonly ReservationService _sut;

    public ReservationServiceTests()
    {
        _reservationRepositoryMock = new Mock<IReservationRepository>();
        _customerRepositoryMock = new Mock<IRepository<Customer>>();
        _vehicleRepositoryMock = new Mock<IRepository<Vehicle>>();
        _vehicleRepositorySpecificMock = new Mock<IVehicleRepository>();
        _officeRepositoryMock = new Mock<IRepository<Office>>();
        _holdServiceMock = new Mock<IReservationHoldService>();
        _applicationDbContextMock = new Mock<IApplicationDbContext>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDatabaseMock = new Mock<IDatabase>();
        _fleetServiceMock = new Mock<IFleetService>();
        _pricingServiceMock = new Mock<IPricingService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _notificationQueueServiceMock = new Mock<INotificationQueueService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<ReservationService>>();

        _applicationDbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _redisMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_redisDatabaseMock.Object);

        _redisDatabaseMock
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _redisDatabaseMock
            .Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
            .ReturnsAsync(true);

        _holdServiceMock
            .Setup(x => x.GetHoldAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationHoldSnapshot?)null);

        _paymentServiceMock
            .Setup(x => x.CreateDepositPreAuthorizationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOperationApiDto
            {
                ReservationId = Guid.NewGuid(),
                PaymentIntentId = Guid.NewGuid(),
                PaymentKind = "DepositPreAuthorization",
                Operation = "CreatePreAuthorization",
                Status = "Succeeded"
            });

        _paymentServiceMock
            .Setup(x => x.ReleaseDepositAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOperationApiDto
            {
                ReservationId = Guid.NewGuid(),
                PaymentIntentId = Guid.NewGuid(),
                PaymentKind = "DepositPreAuthorization",
                Operation = "ReleaseDeposit",
                Status = "Succeeded"
            });

        _paymentServiceMock
            .Setup(x => x.CaptureDepositAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOperationApiDto
            {
                ReservationId = Guid.NewGuid(),
                PaymentIntentId = Guid.NewGuid(),
                PaymentKind = "DepositPreAuthorization",
                Operation = "CapturePreAuthorization",
                Status = "Succeeded"
            });

        _notificationQueueServiceMock
            .Setup(x => x.EnqueueEmailAsync(It.IsAny<QueuedEmailNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _notificationQueueServiceMock
            .Setup(x => x.EnqueueSmsAsync(It.IsAny<QueuedSmsNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _sut = new ReservationService(
            _reservationRepositoryMock.Object,
            _customerRepositoryMock.Object,
            _vehicleRepositoryMock.Object,
            _vehicleRepositorySpecificMock.Object,
            _officeRepositoryMock.Object,
            _holdServiceMock.Object,
            _applicationDbContextMock.Object,
            _fleetServiceMock.Object,
            _pricingServiceMock.Object,
            _paymentServiceMock.Object,
            _notificationQueueServiceMock.Object,
            _memoryCache,
            _redisMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SearchAvailabilityAsync_WhenInvoked_ReturnsUnder300Ms()
    {
        // Arrange
        var pickupOfficeId = Guid.NewGuid();
        var pickup = DateTime.UtcNow.AddDays(5);
        var dropoff = pickup.AddDays(3);
        var groupId = Guid.NewGuid();

        _fleetServiceMock
            .Setup(x => x.SearchAvailableVehicleGroupsAsync(
                pickupOfficeId,
                pickup,
                dropoff,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FleetContracts.AvailableVehicleGroupDto>
            {
                new(groupId, "Ekonomi", "Economy", 6, 850, "TRY", 2000, 21, 2, ["Klima"], null)
            });

        _pricingServiceMock
            .Setup(x => x.CalculateBreakdownAsync(
                groupId,
                pickupOfficeId,
                pickupOfficeId,
                pickup,
                dropoff,
                null,
                0,
                0,
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceBreakdownDto(
                DailyRate: 850,
                RentalDays: 3,
                BaseTotal: 2550,
                ExtrasTotal: 0,
                CampaignDiscount: 0,
                AirportFee: 0,
                OneWayFee: 0,
                ExtraDriverFee: 0,
                ChildSeatFee: 0,
                YoungDriverFee: 0,
                FullCoverageWaiverFee: 0,
                FinalTotal: 2550,
                DepositAmount: 2000,
                PreAuthorizationAmount: 2000,
                Currency: "TRY",
                AppliedCampaignCode: null));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _sut.SearchAvailabilityAsync(
            pickupOfficeId,
            null,
            pickup,
            dropoff,
            null,
            1,
            20,
            CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(1);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300);
    }

    [Fact]
    public async Task SearchAvailabilityAsync_WhenCalledTwice_UsesCacheAndAvoidsRecomputation()
    {
        // Arrange
        var pickupOfficeId = Guid.NewGuid();
        var pickup = DateTime.UtcNow.AddDays(2);
        var dropoff = pickup.AddDays(2);
        var groupId = Guid.NewGuid();

        _fleetServiceMock
            .Setup(x => x.SearchAvailableVehicleGroupsAsync(
                pickupOfficeId,
                pickup,
                dropoff,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FleetContracts.AvailableVehicleGroupDto>
            {
                new(groupId, "SUV", "SUV", 2, 1300, "TRY", 3500, 25, 3, ["Klima", "Otomatik"], null)
            });

        _pricingServiceMock
            .Setup(x => x.CalculateBreakdownAsync(
                groupId,
                pickupOfficeId,
                pickupOfficeId,
                pickup,
                dropoff,
                null,
                0,
                0,
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceBreakdownDto(
                DailyRate: 1300,
                RentalDays: 2,
                BaseTotal: 2600,
                ExtrasTotal: 0,
                CampaignDiscount: 0,
                AirportFee: 0,
                OneWayFee: 0,
                ExtraDriverFee: 0,
                ChildSeatFee: 0,
                YoungDriverFee: 0,
                FullCoverageWaiverFee: 0,
                FinalTotal: 2600,
                DepositAmount: 3500,
                PreAuthorizationAmount: 3500,
                Currency: "TRY",
                AppliedCampaignCode: null));

        // Act
        var first = await _sut.SearchAvailabilityAsync(
            pickupOfficeId,
            null,
            pickup,
            dropoff,
            null,
            1,
            20,
            CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var second = await _sut.SearchAvailabilityAsync(
            pickupOfficeId,
            null,
            pickup,
            dropoff,
            null,
            1,
            20,
            CancellationToken.None);
        stopwatch.Stop();

        // Assert
        second.Should().BeEquivalentTo(first);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300);

        _fleetServiceMock.Verify(x => x.SearchAvailableVehicleGroupsAsync(
                pickupOfficeId,
                pickup,
                dropoff,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _pricingServiceMock.Verify(x => x.CalculateBreakdownAsync(
                groupId,
                pickupOfficeId,
                pickupOfficeId,
                pickup,
                dropoff,
                null,
                0,
                0,
                null,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateDraftReservationAsync_WhenVehicleGroupAvailable_CreatesReservation()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var request = CreateValidReservationRequest() with { VehicleGroupId = groupId };

        _fleetServiceMock.Setup(x => x.SearchAvailableVehicleGroupsAsync(
                request.PickupOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                request.VehicleGroupId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FleetContracts.AvailableVehicleGroupDto>
            {
                new(groupId, "Ekonomi", "Economy", 5, 500, "TRY", 2000, 21, 2, ["Klima"], null)
            });

        _pricingServiceMock.Setup(x => x.CalculateBreakdownAsync(
                request.VehicleGroupId,
                request.PickupOfficeId,
                request.PickupOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                request.CampaignCode,
                request.ExtraDriverCount,
                request.ChildSeatCount,
                request.DriverAge,
                request.FullCoverageWaiver,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceBreakdownDto(
                DailyRate: 500,
                RentalDays: 3,
                BaseTotal: 1500,
                ExtrasTotal: 200,
                CampaignDiscount: 0,
                AirportFee: 0,
                OneWayFee: 0,
                ExtraDriverFee: 0,
                ChildSeatFee: 100,
                YoungDriverFee: 0,
                FullCoverageWaiverFee: 100,
                FinalTotal: 1700,
                DepositAmount: 2000,
                PreAuthorizationAmount: 2000,
                Currency: "TRY",
                AppliedCampaignCode: null));

        // Setup customer repository mock - no existing customer
        _customerRepositoryMock.Setup(x => x.GetQueryable())
            .Returns(new List<Customer>().BuildMockDbSet().Object);

        // Act
        var result = await _sut.CreateDraftReservationAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Draft");
        result.TotalAmount.Should().Be(1700);
        result.CustomerEmail.Should().Be(request.Customer.Email);
        
        _reservationRepositoryMock.Verify(x => x.AddAsync(
            It.Is<Reservation>(r =>
                r.Status == ReservationStatus.Draft
                && r.TotalAmount == 1700
                && r.VehicleId == request.VehicleGroupId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateDraftReservationAsync_WhenExistingCustomerEmailDiffersByCase_ReusesExistingCustomerByNormalizedEmail()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var existingCustomerId = Guid.NewGuid();
        var request = CreateValidReservationRequest() with
        {
            VehicleGroupId = groupId,
            Customer = new CustomerInfoRequest
            {
                FirstName = "Ahmet",
                LastName = "Yilmaz",
                Email = "  AHMET@EXAMPLE.COM  ",
                Phone = "+90 555 123 4567"
            }
        };

        var existingCustomer = new Customer
        {
            Id = existingCustomerId,
            FullName = "Ahmet Yilmaz",
            Email = "ahmet@example.com",
            Phone = "+90 555 123 4567",
            IdentityNumber = string.Empty,
            Nationality = "TR"
        };

        _fleetServiceMock.Setup(x => x.SearchAvailableVehicleGroupsAsync(
                request.PickupOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                request.VehicleGroupId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FleetContracts.AvailableVehicleGroupDto>
            {
                new(groupId, "Ekonomi", "Economy", 5, 500, "TRY", 2000, 21, 2, ["Klima"], null)
            });

        _pricingServiceMock.Setup(x => x.CalculateBreakdownAsync(
                request.VehicleGroupId,
                request.PickupOfficeId,
                request.PickupOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                request.CampaignCode,
                request.ExtraDriverCount,
                request.ChildSeatCount,
                request.DriverAge,
                request.FullCoverageWaiver,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceBreakdownDto(
                DailyRate: 500,
                RentalDays: 3,
                BaseTotal: 1500,
                ExtrasTotal: 200,
                CampaignDiscount: 0,
                AirportFee: 0,
                OneWayFee: 0,
                ExtraDriverFee: 0,
                ChildSeatFee: 100,
                YoungDriverFee: 0,
                FullCoverageWaiverFee: 100,
                FinalTotal: 1700,
                DepositAmount: 2000,
                PreAuthorizationAmount: 2000,
                Currency: "TRY",
                AppliedCampaignCode: null));

        _customerRepositoryMock
            .Setup(x => x.GetQueryable())
            .Returns(new List<Customer> { existingCustomer }.BuildMockDbSet().Object);

        // Act
        var result = await _sut.CreateDraftReservationAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(existingCustomerId);
        result.CustomerEmail.Should().Be(existingCustomer.Email);

        _customerRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _reservationRepositoryMock.Verify(x => x.AddAsync(
            It.Is<Reservation>(r => r.CustomerId == existingCustomerId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateHoldAsync_WhenDraftReservationHasGroup_AssignsVehicleAndCreatesHold()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var sessionId = "session-123";

        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "ABC-1234-DEF",
            CustomerId = Guid.NewGuid(),
            VehicleId = groupId,
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            Status = ReservationStatus.Draft,
            TotalAmount = 1500
        };

        var availableVehicle = new Vehicle
        {
            Id = vehicleId,
            GroupId = groupId,
            Status = VehicleStatus.Available,
            OfficeId = Guid.NewGuid(),
            Plate = "34ABC123",
            Brand = "Renault",
            Model = "Clio"
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _vehicleRepositoryMock
            .Setup(x => x.GetQueryable())
            .Returns(new List<Vehicle> { availableVehicle }.BuildMockDbSet().Object);

        _reservationRepositoryMock
            .Setup(x => x.HasOverlappingReservationsAsync(
                vehicleId,
                reservation.PickupDateTime,
                reservation.ReturnDateTime,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _holdServiceMock
            .Setup(x => x.CreateHoldAsync(
                reservationId,
                vehicleId,
                sessionId,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateHoldAsync(reservationId, sessionId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        reservation.Status.Should().Be(ReservationStatus.Hold);
        reservation.VehicleId.Should().Be(vehicleId);

        _holdServiceMock.Verify(x => x.CreateHoldAsync(
                reservationId,
                vehicleId,
                sessionId,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateHoldAsync_WhenActiveHoldExistsForSameSession_ReturnsExistingHoldWithoutCreatingNewOne()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "ABC-1234-DEF",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            Status = ReservationStatus.Hold,
            TotalAmount = 1500
        };

        var snapshot = new ReservationHoldSnapshot(
            reservationId,
            reservation.VehicleId,
            "session-1",
            DateTime.UtcNow.AddMinutes(8));

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock
            .Setup(x => x.GetHoldAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _sut.CreateHoldAsync(reservationId, "session-1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ReservationId.Should().Be(reservationId);
        result.SessionId.Should().Be("session-1");

        _holdServiceMock.Verify(x => x.CreateHoldAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateHoldAsync_WhenActiveHoldExistsForDifferentSession_ReturnsNull()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "ABC-1234-DEF",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            Status = ReservationStatus.Hold,
            TotalAmount = 1500
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock
            .Setup(x => x.GetHoldAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationHoldSnapshot(
                reservationId,
                reservation.VehicleId,
                "another-session",
                DateTime.UtcNow.AddMinutes(5)));

        // Act
        var result = await _sut.CreateHoldAsync(reservationId, "session-1", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateDraftReservationAsync_WhenVehicleGroupNotAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateValidReservationRequest();

        _fleetServiceMock.Setup(x => x.SearchAvailableVehicleGroupsAsync(
                request.PickupOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                request.VehicleGroupId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FleetContracts.AvailableVehicleGroupDto>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.CreateDraftReservationAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CancelReservationAsync_WhenReservationExistsAndCancellable_CancelsAndReleasesHold()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "ABC-1234-DEF",
            Status = ReservationStatus.Hold,
            CustomerId = Guid.NewGuid(),
            Customer = new Customer { Email = "customer@example.com", Phone = "+905551112233", FullName = "Test User" },
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            TotalAmount = 1500
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock.Setup(x => x.ReleaseHoldAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CancelReservationAsync(reservationId, "Customer requested cancellation");

        // Assert
        result.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        _holdServiceMock.Verify(x => x.ReleaseHoldAsync(reservationId, It.IsAny<CancellationToken>()), Times.Once);
        _notificationQueueServiceMock.Verify(x => x.EnqueueEmailAsync(It.IsAny<QueuedEmailNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationQueueServiceMock.Verify(x => x.EnqueueSmsAsync(It.IsAny<QueuedSmsNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelReservationAsync_WhenReasonContainsNewlines_DoesNotLogRawReason()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var rawReason = "Customer requested cancellation\r\nForged entry";
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Draft,
            CustomerId = Guid.NewGuid(),
            Customer = new Customer { Email = "customer@example.com", Phone = "+905551112233", FullName = "Test User" },
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            TotalAmount = 1500
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock.Setup(x => x.ReleaseHoldAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CancelReservationAsync(reservationId, rawReason);

        // Assert
        result.Should().BeTrue();
        VerifyLogDoesNotContain(rawReason, "Cancelled reservation", "HasReason: True");
    }

    [Fact]
    public async Task AdminCancelReservationAsync_WhenReasonContainsNewlines_DoesNotLogRawReason()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var rawReason = "Admin note\r\nForged entry";
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "RSV-001",
            Status = ReservationStatus.Paid,
            CustomerId = Guid.NewGuid(),
            Customer = new Customer { Email = "customer@example.com", Phone = "+905551112233", FullName = "Test User" },
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            TotalAmount = 1500,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock.Setup(x => x.ReleaseHoldAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AdminCancelReservationAsync(reservationId, rawReason);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(ReservationStatus.Cancelled.ToString());
        VerifyLogDoesNotContain(rawReason, "Admin cancelled reservation", "HasReason: True");
        _notificationQueueServiceMock.Verify(x => x.EnqueueEmailAsync(It.IsAny<QueuedEmailNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _notificationQueueServiceMock.Verify(x => x.EnqueueSmsAsync(It.IsAny<QueuedSmsNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenPaymentConfirmed_QueuesConfirmationAndReminderNotifications()
    {
        var reservationId = Guid.NewGuid();
        var pickupDateTimeUtc = DateTime.UtcNow.AddDays(3);
        var returnDateTimeUtc = pickupDateTimeUtc.AddDays(2);
        var customerId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "RSV-CONFIRM",
            CustomerId = customerId,
            Customer = new Customer
            {
                Id = customerId,
                Email = "ada@example.com",
                Phone = "+905551112233",
                FullName = "Ada Lovelace"
            },
            VehicleId = Guid.NewGuid(),
            PickupDateTime = pickupDateTimeUtc,
            ReturnDateTime = returnDateTimeUtc,
            Status = ReservationStatus.PendingPayment,
            TotalAmount = 1000m
        };

        var paymentIntent = new PaymentIntent
        {
            ReservationId = reservationId,
            Status = PaymentStatus.Succeeded,
            ProviderIntentId = "provider-intent-confirm",
            ProviderTransactionId = "provider-tx-confirm",
            Provider = "Mock",
            IdempotencyKey = "confirm-intent",
            Amount = 1000m
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _applicationDbContextMock
            .Setup(x => x.PaymentIntents)
            .Returns(new List<PaymentIntent> { paymentIntent }.BuildMockDbSet().Object);

        var result = await _sut.ConfirmPaymentAsync(reservationId, "provider-tx-confirm", CancellationToken.None);

        result.Should().NotBeNull();
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.ReservationConfirmed),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueSmsAsync(
                It.Is<QueuedSmsNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.ReservationConfirmed),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.PickupReminder),
                It.Is<DateTime?>(d => d == pickupDateTimeUtc.AddHours(-24)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueSmsAsync(
                It.Is<QueuedSmsNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.ReturnReminder),
                It.Is<DateTime?>(d => d == returnDateTimeUtc.AddHours(-24)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckInAsync_WhenReservationPaid_TransitionsToActive()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Paid,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3)
        };

        var request = new CheckInRequest
        {
            ActualMileage = 5000,
            ActualFuelLevel = 100
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.CheckInAsync(reservationId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Active");
        _paymentServiceMock.Verify(
            x => x.CreateDepositPreAuthorizationAsync(reservationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckInAsync_WhenDepositPreAuthorizationIsSkipped_StillTransitionsToActive()
    {
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Paid,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3)
        };

        var request = new CheckInRequest
        {
            ActualMileage = 5000,
            ActualFuelLevel = 100
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _paymentServiceMock
            .Setup(x => x.CreateDepositPreAuthorizationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOperationApiDto
            {
                ReservationId = reservationId,
                PaymentIntentId = Guid.Empty,
                PaymentKind = "DepositPreAuthorization",
                Operation = "CreatePreAuthorization",
                Status = "Skipped",
                Reason = "Depozito ön provizyonu için başarılı online ödeme kaydı bulunamadı."
            });

        var result = await _sut.CheckInAsync(reservationId, request);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CheckOutAsync_WhenReservationActive_TransitionsToCompleted()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Active,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(-2),
            ReturnDateTime = DateTime.UtcNow
        };

        var request = new CheckOutRequest
        {
            ReturnMileage = 5500,
            ReturnFuelLevel = 80
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.CheckOutAsync(reservationId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        _paymentServiceMock.Verify(
            x => x.ReleaseDepositAsync(reservationId, request.Notes, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckOutAsync_WhenReservationHasDamage_CapturesDeposit()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Active,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(-2),
            ReturnDateTime = DateTime.UtcNow
        };

        var request = new CheckOutRequest
        {
            ReturnMileage = 5600,
            ReturnFuelLevel = 70,
            IsDamaged = true,
            DamageFee = 750,
            Notes = "Front bumper scratch"
        };

        _reservationRepositoryMock.Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.CheckOutAsync(reservationId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        _paymentServiceMock.Verify(
            x => x.CaptureDepositAsync(reservationId, 750, request.Notes, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(ReservationStatus.Draft, ReservationStatus.Hold, true)]
    [InlineData(ReservationStatus.Hold, ReservationStatus.PendingPayment, true)]
    [InlineData(ReservationStatus.PendingPayment, ReservationStatus.Paid, true)]
    [InlineData(ReservationStatus.Paid, ReservationStatus.Active, true)]
    [InlineData(ReservationStatus.Active, ReservationStatus.Completed, true)]
    [InlineData(ReservationStatus.Completed, ReservationStatus.Draft, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Hold, false)]
    public async Task IsValidStatusTransitionAsync_VariousTransitions_ReturnsExpected(
        ReservationStatus current, 
        ReservationStatus target, 
        bool expected)
    {
        // Act
        var result = await _sut.IsValidStatusTransitionAsync(current, target);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task UpdateReservationAsync_WhenReservationIsModifiable_UpdatesDatesAndPreservesExistingAssignments()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var originalVehicleId = Guid.NewGuid();
        var originalCustomer = new Customer { FullName = "Original Customer", Email = "original@example.com", Phone = "+90 555 000 0000" };
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "RSV-UPDATE",
            CustomerId = Guid.NewGuid(),
            Customer = originalCustomer,
            VehicleId = originalVehicleId,
            PickupDateTime = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Hold,
            TotalAmount = 1500m
        };

        var request = new UpdateReservationRequest
        {
            PickupDateTimeUtc = new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc),
            ReturnDateTimeUtc = new DateTime(2026, 4, 5, 9, 0, 0, DateTimeKind.Utc),
            PickupOfficeId = Guid.NewGuid(),
            ReturnOfficeId = Guid.NewGuid(),
            Customer = new CustomerInfoRequest
            {
                FirstName = "Changed",
                LastName = "Customer",
                Email = "changed@example.com",
                Phone = "+90 555 111 1111"
            },
            Notes = "Ignored by current implementation"
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.UpdateReservationAsync(reservationId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.PickupDateTime.Should().Be(request.PickupDateTimeUtc!.Value);
        result.ReturnDateTime.Should().Be(request.ReturnDateTimeUtc!.Value);
        reservation.VehicleId.Should().Be(originalVehicleId);
        reservation.Customer.Should().BeSameAs(originalCustomer);
        _applicationDbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireReservationAsync_WhenReservationIsOnHold_ExpiresReservationAndReleasesHold()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Hold,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock
            .Setup(x => x.ReleaseHoldAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExpireReservationAsync(reservationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(ReservationStatus.Expired.ToString());
        _holdServiceMock.Verify(x => x.ReleaseHoldAsync(reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireReservationAsync_WhenReservationIsPendingPayment_ReturnsNull()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.PendingPayment,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.ExpireReservationAsync(reservationId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        reservation.Status.Should().Be(ReservationStatus.PendingPayment);
        _holdServiceMock.Verify(x => x.ReleaseHoldAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredReservationsAsync_WhenExpiredHoldsExist_ProcessesEachReservation()
    {
        // Arrange
        var firstReservationId = Guid.NewGuid();
        var secondReservationId = Guid.NewGuid();
        var firstReservation = new Reservation
        {
            Id = firstReservationId,
            Status = ReservationStatus.Hold,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };
        var secondReservation = new Reservation
        {
            Id = secondReservationId,
            Status = ReservationStatus.Hold,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(3),
            ReturnDateTime = DateTime.UtcNow.AddDays(4)
        };

        _holdServiceMock
            .Setup(x => x.GetExpiredHoldsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { firstReservationId, secondReservationId });

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(firstReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstReservation);

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(secondReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondReservation);

        _holdServiceMock
            .Setup(x => x.ReleaseHoldAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.ProcessExpiredReservationsAsync(CancellationToken.None);

        // Assert
        firstReservation.Status.Should().Be(ReservationStatus.Expired);
        secondReservation.Status.Should().Be(ReservationStatus.Expired);
        _holdServiceMock.Verify(x => x.ReleaseHoldAsync(firstReservationId, It.IsAny<CancellationToken>()), Times.Once);
        _holdServiceMock.Verify(x => x.ReleaseHoldAsync(secondReservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignVehicleAsync_WhenNoOverlapExists_AssignsVehicle()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Draft,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc)
        };
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            Plate = "34XYZ123",
            Brand = "Renault",
            Model = "Clio",
            Status = VehicleStatus.Available
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _vehicleRepositoryMock
            .Setup(x => x.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        _reservationRepositoryMock
            .Setup(x => x.HasOverlappingReservationsAsync(
                vehicleId,
                reservation.PickupDateTime,
                reservation.ReturnDateTime,
                reservationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.AssignVehicleAsync(reservationId, vehicleId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        reservation.VehicleId.Should().Be(vehicleId);
    }

    [Fact]
    public async Task AssignVehicleAsync_WhenOverlapExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Draft,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc)
        };
        var vehicle = new Vehicle { Id = vehicleId, Plate = "34OVER123", Status = VehicleStatus.Available };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _vehicleRepositoryMock
            .Setup(x => x.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        _reservationRepositoryMock
            .Setup(x => x.HasOverlappingReservationsAsync(
                vehicleId,
                reservation.PickupDateTime,
                reservation.ReturnDateTime,
                reservationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => _sut.AssignVehicleAsync(reservationId, vehicleId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Vehicle has overlapping reservations");
    }

    [Fact]
    public async Task UnassignVehicleAsync_WhenReservationExists_ClearsVehicleId()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Paid,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.UnassignVehicleAsync(reservationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.VehicleId.Should().Be(Guid.Empty);
        reservation.VehicleId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task ExtendHoldAsync_WhenReservationCanBeExtended_ReturnsExtendedHold()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Hold,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _holdServiceMock
            .Setup(x => x.ExtendHoldAsync(
                reservationId,
                TimeSpan.FromMinutes(5),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExtendHoldAsync(reservationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ReservationId.Should().Be(reservationId);
        result.RemainingMinutes.Should().Be(5);
        result.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task CanHoldBeExtendedAsync_WhenReservationStatusIsHold_ReturnsTrue()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.Hold,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.CanHoldBeExtendedAsync(reservationId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanHoldBeExtendedAsync_WhenReservationStatusIsNotHold_ReturnsFalse()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = ReservationStatus.PendingPayment,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.CanHoldBeExtendedAsync(reservationId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ReservationStatus.Draft, ReservationStatus.Hold, true)]
    [InlineData(ReservationStatus.Draft, ReservationStatus.Paid, false)]
    [InlineData(ReservationStatus.Hold, ReservationStatus.PendingPayment, true)]
    [InlineData(ReservationStatus.Hold, ReservationStatus.Completed, false)]
    [InlineData(ReservationStatus.PendingPayment, ReservationStatus.Paid, true)]
    [InlineData(ReservationStatus.PendingPayment, ReservationStatus.Expired, false)]
    [InlineData(ReservationStatus.Paid, ReservationStatus.Active, true)]
    [InlineData(ReservationStatus.Paid, ReservationStatus.Hold, false)]
    [InlineData(ReservationStatus.Active, ReservationStatus.Completed, true)]
    [InlineData(ReservationStatus.Active, ReservationStatus.Draft, false)]
    public async Task TransitionStatusAsync_WhenTransitionRequested_ReturnsExpectedOutcome(
        ReservationStatus currentStatus,
        ReservationStatus targetStatus,
        bool shouldSucceed)
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            Status = currentStatus,
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2)
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.TransitionStatusAsync(reservationId, targetStatus, CancellationToken.None);

        // Assert
        if (shouldSucceed)
        {
            result.Should().NotBeNull();
            result!.Status.Should().Be(targetStatus.ToString());
            reservation.Status.Should().Be(targetStatus);
            return;
        }

        result.Should().BeNull();
        reservation.Status.Should().Be(currentStatus);
    }

    [Fact]
    public async Task CreateHoldAsync_WhenConcurrentUpdateOccurs_ThrowsUserFriendlyConflict()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            PublicCode = "RSV-CONFLICT",
            CustomerId = Guid.NewGuid(),
            VehicleId = groupId,
            PickupDateTime = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Draft,
            TotalAmount = 1500m
        };
        var availableVehicle = new Vehicle
        {
            Id = vehicleId,
            GroupId = groupId,
            Status = VehicleStatus.Available,
            OfficeId = Guid.NewGuid(),
            Plate = "34CON123",
            Brand = "Renault",
            Model = "Clio"
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _vehicleRepositoryMock
            .Setup(x => x.GetQueryable())
            .Returns(new List<Vehicle> { availableVehicle }.BuildMockDbSet().Object);

        _reservationRepositoryMock
            .Setup(x => x.HasOverlappingReservationsAsync(
                vehicleId,
                reservation.PickupDateTime,
                reservation.ReturnDateTime,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _applicationDbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("conflict"));

        // Act
        var act = () => _sut.CreateHoldAsync(reservationId, "session-conflict", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Reservation was updated by another request. Please retry.");
    }

    private void VerifyLogDoesNotContain(string forbiddenText, string requiredPrefix, string requiredMetadata)
    {
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(requiredPrefix, StringComparison.Ordinal) &&
                    state.ToString()!.Contains(requiredMetadata, StringComparison.Ordinal) &&
                    !state.ToString()!.Contains(forbiddenText, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static CreateReservationRequest CreateValidReservationRequest() => new()
    {
        VehicleGroupId = Guid.NewGuid(),
        PickupOfficeId = Guid.NewGuid(),
        ReturnOfficeId = Guid.Empty,
        PickupDateTimeUtc = DateTime.UtcNow.AddDays(1),
        ReturnDateTimeUtc = DateTime.UtcNow.AddDays(4),
        Customer = new CustomerInfoRequest
        {
            FirstName = "Ahmet",
            LastName = "Yilmaz",
            Email = "ahmet@example.com",
            Phone = "+90 555 123 4567"
        }
    };

    #region GetCustomerReservationsPaginatedAsync Tests

    [Fact]
    public async Task GetCustomerReservationsPaginatedAsync_ReturnsCorrectPagination()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var reservations = new List<Reservation>
        {
            CreateTestReservation(customerId, "RES001"),
            CreateTestReservation(customerId, "RES002"),
            CreateTestReservation(customerId, "RES003")
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByCustomerIdPaginatedAsync(customerId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reservations, 3));

        // Act
        var result = await _sut.GetCustomerReservationsPaginatedAsync(customerId, 1, 20);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(1);
        result.CurrentPage.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetCustomerReservationsPaginatedAsync_WithMultiplePages_CalculatesCorrectly()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var reservations = new List<Reservation>
        {
            CreateTestReservation(customerId, "RES001"),
            CreateTestReservation(customerId, "RES002")
        };

        _reservationRepositoryMock
            .Setup(x => x.GetByCustomerIdPaginatedAsync(customerId, 2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reservations, 5));

        // Act
        var result = await _sut.GetCustomerReservationsPaginatedAsync(customerId, 2, 2);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.CurrentPage.Should().Be(2);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(150, 100)]
    public async Task GetCustomerReservationsPaginatedAsync_ClampsInvalidPageSize(int inputPageSize, int expectedPageSize)
    {
        // Arrange
        var customerId = Guid.NewGuid();

        _reservationRepositoryMock
            .Setup(x => x.GetByCustomerIdPaginatedAsync(customerId, 1, expectedPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Reservation>(), 0));

        // Act
        var result = await _sut.GetCustomerReservationsPaginatedAsync(customerId, 1, inputPageSize);

        // Assert
        result.PageSize.Should().Be(expectedPageSize);
    }

    private static Reservation CreateTestReservation(Guid customerId, string publicCode) => new()
    {
        Id = Guid.NewGuid(),
        PublicCode = publicCode,
        CustomerId = customerId,
        PickupDateTime = DateTime.UtcNow.AddDays(1),
        ReturnDateTime = DateTime.UtcNow.AddDays(3),
        Status = ReservationStatus.Paid,
        TotalAmount = 1000,
        CreatedAt = DateTime.UtcNow
    };

    #endregion
}
