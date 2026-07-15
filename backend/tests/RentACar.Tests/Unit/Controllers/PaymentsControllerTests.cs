using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Payments;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class PaymentsControllerTests
{
    [Fact]
    public async Task CreateIntent_WhenPaymentsDisabled_ReturnsServiceUnavailableWithoutCallingService()
    {
        var serviceMock = new Mock<IPaymentService>(MockBehavior.Strict);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.CreateIntent(
            new CreatePaymentIntentApiRequest
            {
                ReservationId = Guid.NewGuid(),
                IdempotencyKey = "disabled-create-intent"
            },
            CancellationToken.None);

        AssertPaymentsDisabled(result);
        serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteThreeDs_WhenPaymentsDisabled_ReturnsServiceUnavailableWithoutCallingService()
    {
        var serviceMock = new Mock<IPaymentService>(MockBehavior.Strict);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.CompleteThreeDs(
            Guid.NewGuid(),
            new ThreeDsReturnApiRequest { BankResponse = "forged-success" },
            CancellationToken.None);

        AssertPaymentsDisabled(result);
        serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleWebhook_WhenPaymentsDisabled_ReturnsServiceUnavailableWithoutCallingService()
    {
        var serviceMock = new Mock<IPaymentService>(MockBehavior.Strict);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.HandleWebhook("Mock", CancellationToken.None);

        AssertPaymentsDisabled(result);
        serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleWebhook_WhenPaymentsEnabled_ProcessesSignedPayload()
    {
        var serviceMock = new Mock<IPaymentService>();
        serviceMock
            .Setup(service => service.ProcessWebhookAsync(
                "Iyzico",
                "{\"status\":\"SUCCESS\"}",
                "valid-signature",
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessApiDto { Processed = true });
        var controller = CreateController(serviceMock.Object, enablePayments: true);
        controller.Request.Body = new MemoryStream("{\"status\":\"SUCCESS\"}"u8.ToArray());
        controller.Request.Headers["X-Webhook-Signature"] = "valid-signature";

        var result = await controller.HandleWebhook("Iyzico", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        serviceMock.VerifyAll();
    }

    private static PaymentsController CreateController(
        IPaymentService service,
        bool enablePayments = false)
    {
        return new PaymentsController(
            service,
            Options.Create(new PaymentOptions { EnablePayments = enablePayments }))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static void AssertPaymentsDisabled(IActionResult result)
    {
        var unavailable = result.Should().BeOfType<ObjectResult>().Subject;
        unavailable.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var response = unavailable.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }
}
