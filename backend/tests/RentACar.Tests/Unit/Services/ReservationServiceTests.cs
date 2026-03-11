using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using System.Diagnostics;
using FleetContracts = RentACar.API.Contracts.Fleet;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
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
    private readonly Mock<IFleetService> _fleetServiceMock;
    private readonly Mock<IPricingService> _pricingServiceMock;
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
        _fleetServiceMock = new Mock<IFleetService>();
        _pricingServiceMock = new Mock<IPricingService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<ReservationService>>();

        _applicationDbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _holdServiceMock
            .Setup(x => x.GetHoldAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationHoldSnapshot?)null);

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
            _memoryCache,
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
}


