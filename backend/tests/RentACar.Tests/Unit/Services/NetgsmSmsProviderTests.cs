using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class NetgsmSmsProviderTests
{
    [Fact]
    public async Task SendAsync_WhenBodyContainsCDataTerminator_EscapesXmlBody()
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(new SmsMessageRequest
        {
            ToPhoneNumber = "05551112233",
            Body = "code ]]> more & <tag>"
        });

        result.Success.Should().BeTrue();
        handler.LastRequestBody.Should().NotBeNull();
        handler.LastRequestBody.Should().Contain("<msg>code ]]&gt; more &amp; &lt;tag&gt;</msg>");
        handler.LastRequestBody.Should().Contain("<no>+905551112233</no>");
        handler.LastRequestBody.Should().NotContain("<![CDATA[");
    }

    [Fact]
    public async Task SendAsync_WhenProviderIsNotConfigured_ReturnsConfigurationFailure()
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler, new NotificationOptions
        {
            Sms = new SmsNotificationOptions
            {
                Netgsm = new NetgsmSmsOptions
                {
                    Usercode = string.Empty,
                    Password = string.Empty,
                    MsgHeader = string.Empty,
                    BaseUrl = "https://api.netgsm.test",
                    DefaultEncoding = "TR"
                }
            }
        });

        var result = await sut.SendAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Netgsm");
        result.FailureCode.Should().Be("SMS_NOT_CONFIGURED");
        result.FailureMessage.Should().Be("Netgsm SMS provider is not configured.");
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_WhenPhoneNumberIsInvalid_ReturnsValidationFailure()
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest(phoneNumber: "123"));

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Netgsm");
        result.FailureCode.Should().Be("SMS_INVALID_PHONE");
        result.FailureMessage.Should().Be("Phone number is not valid for Netgsm delivery.");
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_WhenHttpStatusIsNotSuccessful_ReturnsHttpFailure()
    {
        var handler = new CapturingHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
            {
                Content = new StringContent("gateway failed")
            }
        };
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Netgsm");
        result.FailureCode.Should().Be("NETGSM_HTTP_502");
        result.FailureMessage.Should().Be("gateway failed");
    }

    [Fact]
    public async Task SendAsync_WhenHttpClientThrows_ReturnsSendFailure()
    {
        var handler = new CapturingHttpMessageHandler
        {
            ExceptionToThrow = new InvalidOperationException("boom")
        };
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Netgsm");
        result.FailureCode.Should().Be("NETGSM_SEND_FAILED");
        result.FailureMessage.Should().Be("boom");
    }

    [Theory]
    [InlineData("5551112233", "+905551112233")]
    [InlineData("05551112233", "+905551112233")]
    [InlineData("905551112233", "+905551112233")]
    [InlineData("+905551112233", "+905551112233")]
    public async Task SendAsync_NormalizesSupportedPhoneFormats(string phoneNumber, string expectedRecipient)
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest(phoneNumber: phoneNumber));

        result.Success.Should().BeTrue();
        handler.LastRequestBody.Should().Contain($"<no>{expectedRecipient}</no>");
    }

    private static SmsMessageRequest CreateRequest(string phoneNumber = "05551112233", string body = "test")
    {
        return new SmsMessageRequest
        {
            ToPhoneNumber = phoneNumber,
            Body = body
        };
    }

    private static NetgsmSmsProvider CreateSut(
        CapturingHttpMessageHandler handler,
        NotificationOptions? options = null)
    {
        options ??= new NotificationOptions
        {
            Sms = new SmsNotificationOptions
            {
                Netgsm = new NetgsmSmsOptions
                {
                    Usercode = "user",
                    Password = "pass",
                    MsgHeader = "HEADER",
                    BaseUrl = "https://api.netgsm.test",
                    DefaultEncoding = "TR"
                }
            }
        };

        return new NetgsmSmsProvider(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<NetgsmSmsProvider>.Instance);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        public int RequestCount { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; init; }
        public Exception? ExceptionToThrow { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return ResponseFactory?.Invoke(request) ?? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("message-id")
            };
        }
    }
}
