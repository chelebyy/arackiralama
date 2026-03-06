using FluentAssertions;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using Xunit;

namespace RentACar.Tests.Unit.Enums;

public class StatusEnumTests
{
    [Fact]
    public void Vehicle_DefaultsToAvailableStatus()
    {
        var vehicle = new Vehicle();

        vehicle.Status.Should().Be(VehicleStatus.Available);
        Enum.Parse<VehicleStatus>(nameof(VehicleStatus.Available)).Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void PaymentIntent_DefaultsToCreatedStatus()
    {
        var paymentIntent = new PaymentIntent();

        paymentIntent.Status.Should().Be(PaymentStatus.Created);
        Enum.Parse<PaymentStatus>(nameof(PaymentStatus.Created)).Should().Be(PaymentStatus.Created);
    }

    [Fact]
    public void BackgroundJob_DefaultsToPendingStatus()
    {
        var backgroundJob = new BackgroundJob();

        backgroundJob.Status.Should().Be(BackgroundJobStatus.Pending);
        Enum.Parse<BackgroundJobStatus>(nameof(BackgroundJobStatus.Pending)).Should().Be(BackgroundJobStatus.Pending);
    }
}
