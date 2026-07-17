using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Entities;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class ReservationsControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WhenLegacyExtraQuantityIsInvalid_ReturnsBadRequest(bool unpaid)
    {
        var service = new Mock<IReservationService>();
        service.Setup(item => item.CreateDraftReservationAsync(
                It.IsAny<CreateReservationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Legacy extra quantities cannot be negative."));
        service.Setup(item => item.CreateUnpaidRequestAsync(
                It.IsAny<CreateReservationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Legacy extra quantities cannot be negative."));
        var controller = new ReservationsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        var request = CreateValidRequest() with { ChildSeatCount = -1 };

        var result = unpaid
            ? await controller.CreateUnpaidRequest(request, CancellationToken.None)
            : await controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetByPublicCode_WhenCodeExceedsStorageLimit_ReturnsNotFoundWithoutQuerying()
    {
        var service = new Mock<IReservationService>();
        var controller = new ReservationsController(service.Object);

        var result = await controller.GetByPublicCode(
            new string('A', Reservation.PublicCodeMaxLength + 1),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
        service.Verify(item => item.GetReservationByPublicCodeAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByPublicCode_WhenCodeIsAtStorageLimit_QueriesReservation()
    {
        var publicCode = new string('A', Reservation.PublicCodeMaxLength);
        var service = new Mock<IReservationService>();
        service.Setup(item => item.GetReservationByPublicCodeAsync(
                publicCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicReservationSummaryDto { PublicCode = publicCode });
        var controller = new ReservationsController(service.Object);

        var result = await controller.GetByPublicCode(publicCode, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        service.Verify(item => item.GetReservationByPublicCodeAsync(
            publicCode,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreateReservationRequest CreateValidRequest()
    {
        var pickup = new DateTime(2027, 8, 10, 10, 0, 0, DateTimeKind.Utc);
        return new CreateReservationRequest
        {
            VehicleGroupId = Guid.NewGuid(),
            PickupOfficeId = Guid.NewGuid(),
            ReturnOfficeId = Guid.NewGuid(),
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            Customer = new CustomerInfoRequest
            {
                FirstName = "Legacy",
                LastName = "Validation",
                Email = "legacy-validation@example.test",
                Phone = "+905551234567"
            }
        };
    }
}
