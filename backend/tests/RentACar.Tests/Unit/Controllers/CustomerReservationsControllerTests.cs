using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Authentication;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public class CustomerReservationsControllerTests
{
    #region GetMyReservations Tests

    [Fact]
    public async Task GetMyReservations_WithAuthenticatedCustomer_ReturnsPaginatedResult()
    {
        var customerId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetCustomerReservationsPaginatedAsync(customerId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponse<ReservationDto>
            {
                Items = new List<ReservationDto>
                {
                    new() { Id = Guid.NewGuid(), CustomerId = customerId, PublicCode = "RES001" },
                    new() { Id = Guid.NewGuid(), CustomerId = customerId, PublicCode = "RES002" }
                },
                TotalCount = 2,
                TotalPages = 1,
                CurrentPage = 1,
                PageSize = 20
            });

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.GetMyReservations(1, 20, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        mockService.Verify(s => s.GetCustomerReservationsPaginatedAsync(customerId, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyReservations_WithoutAuthentication_ReturnsUnauthorized()
    {
        var mockService = new Mock<IReservationService>();
        var controller = CreateController(mockService.Object);

        var result = await controller.GetMyReservations(1, 20, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        mockService.Verify(s => s.GetCustomerReservationsPaginatedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyReservations_WithCustomPageSize_PassesCorrectParameters()
    {
        var customerId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetCustomerReservationsPaginatedAsync(customerId, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponse<ReservationDto>
            {
                Items = new List<ReservationDto>(),
                TotalCount = 15,
                TotalPages = 2,
                CurrentPage = 2,
                PageSize = 10
            });

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.GetMyReservations(2, 10, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        mockService.Verify(s => s.GetCustomerReservationsPaginatedAsync(customerId, 2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetMyReservation Tests

    [Fact]
    public async Task GetMyReservation_WithOwnReservation_ReturnsReservation()
    {
        var customerId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationDto { Id = reservationId, CustomerId = customerId, PublicCode = "RES001" });

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.GetMyReservation(reservationId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        mockService.Verify(s => s.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyReservation_WithOtherCustomerReservation_ReturnsNotFound()
    {
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationDto { Id = reservationId, CustomerId = otherCustomerId, PublicCode = "RES001" });

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.GetMyReservation(reservationId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMyReservation_WithNonExistentReservation_ReturnsNotFound()
    {
        var customerId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationDto?)null);

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.GetMyReservation(reservationId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CancelMyReservation Tests

    [Fact]
    public async Task CancelMyReservation_WithOwnReservation_CancelsSuccessfully()
    {
        var customerId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationDto { Id = reservationId, CustomerId = customerId, PublicCode = "RES001", Status = "Draft" });
        mockService
            .Setup(s => s.CancelReservationAsync(reservationId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.CancelMyReservation(reservationId, "Changed mind", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        mockService.Verify(s => s.CancelReservationAsync(reservationId, "Changed mind", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelMyReservation_WithOtherCustomerReservation_ReturnsNotFound()
    {
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var mockService = new Mock<IReservationService>();
        mockService
            .Setup(s => s.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReservationDto { Id = reservationId, CustomerId = otherCustomerId, PublicCode = "RES001" });

        var controller = CreateController(mockService.Object);
        SetCustomerPrincipal(controller, customerId);

        var result = await controller.CancelMyReservation(reservationId, "Reason", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
        mockService.Verify(s => s.CancelReservationAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelMyReservation_WithoutAuthentication_ReturnsUnauthorized()
    {
        var mockService = new Mock<IReservationService>();
        var controller = CreateController(mockService.Object);

        var result = await controller.CancelMyReservation(Guid.NewGuid(), "Reason", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Helper Methods

    private static CustomerReservationsController CreateController(IReservationService reservationService)
    {
        return new CustomerReservationsController(reservationService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static void SetCustomerPrincipal(CustomerReservationsController controller, Guid customerId)
    {
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, AuthRoleNames.Customer),
                new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString())
            ],
            authenticationType: "Bearer"));
    }

    #endregion
}
