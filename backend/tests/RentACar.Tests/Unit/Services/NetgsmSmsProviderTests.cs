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
        var httpClient = new HttpClient(handler);
        var sut = new NetgsmSmsProvider(
            httpClient,
            Options.Create(new NotificationOptions
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
            }),
            NullLogger<NetgsmSmsProvider>.Instance);

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

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("message-id")
            };
        }
    }
}
