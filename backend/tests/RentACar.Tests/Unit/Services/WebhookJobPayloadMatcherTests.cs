using FluentAssertions;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class WebhookJobPayloadMatcherTests
{
    [Fact]
    public void HasProviderEventId_WhenPayloadMatches_ReturnsTrue()
    {
        var payload = """{"ProviderEventId":"evt-123","ReservationId":"5d8d64b8-61b1-4c1a-9e36-93d8e2d61dd2"}""";

        var result = WebhookJobPayloadMatcher.HasProviderEventId(payload, "evt-123");

        result.Should().BeTrue();
    }

    [Fact]
    public void HasProviderEventId_WhenPayloadIsInvalid_ReturnsFalse()
    {
        var result = WebhookJobPayloadMatcher.HasProviderEventId("not-json", "evt-123");

        result.Should().BeFalse();
    }
}
