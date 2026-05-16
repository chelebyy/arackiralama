using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class TwilioSmsProviderTests
{
    [Fact]
    public async Task SendAsync_WhenProviderIsNotConfigured_ReturnsConfigurationFailure()
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler, new NotificationOptions
        {
            Sms = new SmsNotificationOptions
            {
                Twilio = new TwilioSmsOptions
                {
                    AccountSid = string.Empty,
                    AuthToken = string.Empty,
                    FromPhoneNumber = string.Empty
                }
            }
        });

        var result = await sut.SendAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Twilio");
        result.FailureCode.Should().Be("SMS_NOT_CONFIGURED");
        result.FailureMessage.Should().Be("Twilio SMS provider is not configured.");
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_WhenPhoneNumberIsInvalid_ReturnsValidationFailure()
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest(phoneNumber: "123"));

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Twilio");
        result.FailureCode.Should().Be("SMS_INVALID_PHONE");
        result.FailureMessage.Should().Be("Phone number is not valid for Twilio delivery.");
        handler.RequestCount.Should().Be(0);
    }

    [Theory]
    [InlineData("5551112233", "+905551112233")]
    [InlineData("05551112233", "+905551112233")]
    [InlineData("+447700900123", "+447700900123")]
    public async Task SendAsync_NormalizesSupportedPhoneFormats(string phoneNumber, string expectedRecipient)
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest(phoneNumber: phoneNumber));

        result.Success.Should().BeTrue();
        handler.LastFormValues.Should().NotBeNull();
        handler.LastFormValues!["To"].Should().Be(expectedRecipient);
    }

    [Fact]
    public async Task SendAsync_WhenRequestSucceeds_SendsExpectedFormAndBasicAuth()
    {
        var handler = new CapturingHttpMessageHandler();
        var sut = CreateSut(handler, new NotificationOptions
        {
            Sms = new SmsNotificationOptions
            {
                Twilio = new TwilioSmsOptions
                {
                    AccountSid = "acct-123",
                    AuthToken = "secret-token",
                    FromPhoneNumber = "+905000000000",
                    StatusCallbackUrl = "https://example.test/twilio/status"
                }
            }
        });

        var result = await sut.SendAsync(CreateRequest(body: "reservation confirmed"));

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Twilio");
        result.ProviderMessageId.Should().Be("SM123");
        handler.LastRequestUri.Should().Be("https://api.twilio.com/2010-04-01/Accounts/acct-123/Messages.json");
        handler.LastAuthorizationScheme.Should().Be("Basic");
        handler.LastAuthorizationParameter.Should().Be(Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("acct-123:secret-token")));
        handler.LastFormValues.Should().NotBeNull();
        handler.LastFormValues!["To"].Should().Be("+905551112233");
        handler.LastFormValues["From"].Should().Be("+905000000000");
        handler.LastFormValues["Body"].Should().Be("reservation confirmed");
        handler.LastFormValues["StatusCallback"].Should().Be("https://example.test/twilio/status");
    }

    [Fact]
    public async Task SendAsync_WhenHttpStatusIsNotSuccessful_ReturnsTwilioErrorPayload()
    {
        var handler = new CapturingHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"code\":\"21608\",\"message\":\"permission denied\"}")
            }
        };
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Twilio");
        result.FailureCode.Should().Be("21608");
        result.FailureMessage.Should().Be("permission denied");
    }

    [Fact]
    public async Task SendAsync_WhenHttpFailurePayloadHasNoCode_UsesStatusCodeFallback()
    {
        var handler = new CapturingHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("{\"message\":\"gateway failed\"}")
            }
        };
        var sut = CreateSut(handler);

        var result = await sut.SendAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.Provider.Should().Be("Twilio");
        result.FailureCode.Should().Be("TWILIO_HTTP_502");
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
        result.Provider.Should().Be("Twilio");
        result.FailureCode.Should().Be("TWILIO_SEND_FAILED");
        result.FailureMessage.Should().Be("boom");
    }

    private static SmsMessageRequest CreateRequest(string phoneNumber = "05551112233", string body = "test")
    {
        return new SmsMessageRequest
        {
            ToPhoneNumber = phoneNumber,
            Body = body
        };
    }

    private static TwilioSmsProvider CreateSut(CapturingHttpMessageHandler handler, NotificationOptions? options = null)
    {
        options ??= new NotificationOptions
        {
            Sms = new SmsNotificationOptions
            {
                Twilio = new TwilioSmsOptions
                {
                    AccountSid = "test-account",
                    AuthToken = "test-token",
                    FromPhoneNumber = "+905000000000"
                }
            }
        };

        return new TwilioSmsProvider(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<TwilioSmsProvider>.Instance);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? LastRequestUri { get; private set; }
        public string? LastAuthorizationScheme { get; private set; }
        public string? LastAuthorizationParameter { get; private set; }
        public Dictionary<string, string>? LastFormValues { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; init; }
        public Exception? ExceptionToThrow { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri?.ToString();
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastAuthorizationParameter = request.Headers.Authorization?.Parameter;

            if (request.Content is not null)
            {
                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                LastFormValues = requestBody
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(segment => segment.Split('=', 2))
                    .ToDictionary(
                        parts => Uri.UnescapeDataString(parts[0]),
                        parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty);
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return ResponseFactory?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sid\":\"SM123\"}")
            };
        }
    }
}
