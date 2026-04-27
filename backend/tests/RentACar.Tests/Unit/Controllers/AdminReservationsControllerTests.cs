using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Enums;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminReservationsControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsReservations()
    {
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetAllReservationsAsync(It.IsAny<ReservationFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReservationDto> { CreateReservationDto() });

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.GetAll(null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<IReadOnlyList<ReservationDto>>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsReservation()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.GetById(id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<ReservationDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationDto?)null);

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByCustomerId_ReturnsReservations()
    {
        var customerId = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetCustomerReservationsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReservationDto> { CreateReservationDto() });

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.GetByCustomerId(customerId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<IReadOnlyList<ReservationDto>>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WhenFound_ReturnsUpdatedReservation()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.UpdateReservationAsync(id, It.IsAny<UpdateReservationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));

        var auditLogMock = new Mock<IAuditLogService>();
        var controller = CreateController(reservationServiceMock.Object, auditLogService: auditLogMock.Object);

        var result = await controller.Update(id, new UpdateReservationRequest { Notes = "Updated" }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<ReservationDto>>().Subject;
        response.Success.Should().BeTrue();
        auditLogMock.Verify(a => a.LogAsync("Update", "Reservation", id.ToString(), "admin-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationDto?)null);

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.Update(Guid.NewGuid(), new UpdateReservationRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.UpdateReservationAsync(id, It.IsAny<UpdateReservationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid dates"));

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.Update(id, new UpdateReservationRequest(), CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AssignVehicle_WhenEmptyVehicleId_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IReservationService>());

        var result = await controller.AssignVehicle(Guid.NewGuid(), Guid.Empty, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AssignVehicle_WhenFound_ReturnsAssignedReservation()
    {
        var id = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.AssignVehicleAsync(id, vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id, vehicleId: vehicleId));

        var auditLogMock = new Mock<IAuditLogService>();
        var controller = CreateController(reservationServiceMock.Object, auditLogService: auditLogMock.Object);

        var result = await controller.AssignVehicle(id, vehicleId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<ReservationDto>>().Subject;
        response.Success.Should().BeTrue();
        auditLogMock.Verify(a => a.LogAsync("AssignVehicle", "Reservation", id.ToString(), "admin-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignVehicle_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.AssignVehicleAsync(id, vehicleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vehicle not available"));

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.AssignVehicle(id, vehicleId, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UnassignVehicle_WhenFound_ReturnsUnassignedReservation()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id, vehicleId: Guid.NewGuid()));
        reservationServiceMock.Setup(s => s.UnassignVehicleAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));

        var auditLogMock = new Mock<IAuditLogService>();
        var controller = CreateController(reservationServiceMock.Object, auditLogService: auditLogMock.Object);

        var result = await controller.UnassignVehicle(id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<ReservationDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UnassignVehicle_WhenNotFound_ReturnsNotFound()
    {
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationDto?)null);

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.UnassignVehicle(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TransitionStatus_WithEmptyStatus_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IReservationService>());

        var result = await controller.TransitionStatus(Guid.NewGuid(), "   ", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransitionStatus_WithInvalidStatus_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IReservationService>());

        var result = await controller.TransitionStatus(Guid.NewGuid(), "InvalidStatus", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransitionStatus_WhenFound_ReturnsTransitionedReservation()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.TransitionStatusAsync(id, ReservationStatus.Completed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id, status: ReservationStatus.Completed));

        var auditLogMock = new Mock<IAuditLogService>();
        var controller = CreateController(reservationServiceMock.Object, auditLogService: auditLogMock.Object);

        var result = await controller.TransitionStatus(id, "Completed", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<ReservationDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionStatus_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.TransitionStatusAsync(id, ReservationStatus.Cancelled, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot cancel"));

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.TransitionStatus(id, "Cancelled", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_WhenFound_ReturnsCancelledReservation()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.AdminCancelReservationAsync(id, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id, status: ReservationStatus.Cancelled));

        var auditLogMock = new Mock<IAuditLogService>();
        var controller = CreateController(reservationServiceMock.Object, auditLogService: auditLogMock.Object);

        var result = await controller.Cancel(id, "Customer request", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<ReservationDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_WhenNotFound_ReturnsNotFound()
    {
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationDto?)null);

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.Cancel(Guid.NewGuid(), null, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Cancel_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.GetReservationByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservationDto(id));
        reservationServiceMock.Setup(s => s.AdminCancelReservationAsync(id, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot cancel paid reservation"));

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.Cancel(id, null, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessExpired_ReturnsOk()
    {
        var reservationServiceMock = new Mock<IReservationService>();
        reservationServiceMock.Setup(s => s.ProcessExpiredReservationsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(reservationServiceMock.Object);

        var result = await controller.ProcessExpired(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();
    }

    private static ReservationDto CreateReservationDto(
        Guid? id = null,
        ReservationStatus status = ReservationStatus.Paid,
        Guid? vehicleId = null)
    {
        return new ReservationDto
        {
            Id = id ?? Guid.NewGuid(),
            PublicCode = "ABC123",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Customer",
            CustomerEmail = "test@test.com",
            CustomerPhone = "+905551234567",
            VehicleId = vehicleId ?? Guid.Empty,
            VehiclePlate = "07ABC001",
            VehicleBrand = "Toyota",
            VehicleModel = "Corolla",
            VehicleGroupId = Guid.NewGuid(),
            VehicleGroupName = "Economy",
            PickupOfficeId = Guid.NewGuid(),
            PickupOfficeName = "Merkez",
            ReturnOfficeId = Guid.NewGuid(),
            ReturnOfficeName = "Havalimani",
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            Status = status.ToString(),
            TotalAmount = 1000m,
            DepositAmount = 500m,
            RentalDays = 2,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static AdminReservationsController CreateController(
        IReservationService reservationService,
        IPaymentService? paymentService = null,
        IAuditLogService? auditLogService = null)
    {
        paymentService ??= Mock.Of<IPaymentService>();
        auditLogService ??= Mock.Of<IAuditLogService>();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim(ClaimTypes.Name, "admin-1"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        return new AdminReservationsController(reservationService, paymentService, auditLogService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }
}
