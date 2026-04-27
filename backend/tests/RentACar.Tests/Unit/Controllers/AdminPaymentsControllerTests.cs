using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Payments;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminPaymentsControllerTests
{
    [Fact]
    public async Task RetryPayment_WithEmptyReservationId_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IPaymentService>());

        var result = await controller.RetryPayment(
            new AdminPaymentRetryApiRequest { ReservationId = Guid.Empty },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryPayment_WithEmptyCardNumber_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IPaymentService>());

        var result = await controller.RetryPayment(
            new AdminPaymentRetryApiRequest
            {
                ReservationId = Guid.NewGuid(),
                Card = new PaymentCardApiRequest { HolderName = "Test", Number = "" }
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryPayment_WithEmptyHolderName_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IPaymentService>());

        var result = await controller.RetryPayment(
            new AdminPaymentRetryApiRequest
            {
                ReservationId = Guid.NewGuid(),
                Card = new PaymentCardApiRequest { HolderName = "", Number = "411111" }
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryPayment_Success_ReturnsOk()
    {
        var serviceMock = new Mock<IPaymentService>();
        serviceMock.Setup(s => s.RetryPaymentAsync(It.IsAny<AdminPaymentRetryApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentApiDto { PaymentIntentId = Guid.NewGuid(), Status = "success" });

        var controller = CreateController(serviceMock.Object);
        var request = new AdminPaymentRetryApiRequest
        {
            ReservationId = Guid.NewGuid(),
            Card = new PaymentCardApiRequest { HolderName = "Test", Number = "411111" }
        };

        var result = await controller.RetryPayment(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PaymentIntentApiDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RetryPayment_WhenServiceReturnsNull_ReturnsNotFound()
    {
        var serviceMock = new Mock<IPaymentService>();
        serviceMock.Setup(s => s.RetryPaymentAsync(It.IsAny<AdminPaymentRetryApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentIntentApiDto?)null);

        var controller = CreateController(serviceMock.Object);
        var request = new AdminPaymentRetryApiRequest
        {
            ReservationId = Guid.NewGuid(),
            Card = new PaymentCardApiRequest { HolderName = "Test", Number = "411111" }
        };

        var result = await controller.RetryPayment(request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RetryPayment_WhenServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var serviceMock = new Mock<IPaymentService>();
        serviceMock.Setup(s => s.RetryPaymentAsync(It.IsAny<AdminPaymentRetryApiRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payment already completed"));

        var controller = CreateController(serviceMock.Object);
        var request = new AdminPaymentRetryApiRequest
        {
            ReservationId = Guid.NewGuid(),
            Card = new PaymentCardApiRequest { HolderName = "Test", Number = "411111" }
        };

        var result = await controller.RetryPayment(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetPaymentStatus_WithEmptyGuid_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IPaymentService>());

        var result = await controller.GetPaymentStatus(Guid.Empty, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPaymentStatus_WhenNotFound_ReturnsNotFound()
    {
        var serviceMock = new Mock<IPaymentService>();
        serviceMock.Setup(s => s.GetPaymentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminPaymentStatusApiDto?)null);

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetPaymentStatus(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPaymentStatus_Success_ReturnsOk()
    {
        var serviceMock = new Mock<IPaymentService>();
        serviceMock.Setup(s => s.GetPaymentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminPaymentStatusApiDto { PaymentIntentId = Guid.NewGuid(), InternalStatus = "completed" });

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetPaymentStatus(Guid.NewGuid(), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPaymentStatusApiDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    private static AdminPaymentsController CreateController(IPaymentService service)
    {
        return new AdminPaymentsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
