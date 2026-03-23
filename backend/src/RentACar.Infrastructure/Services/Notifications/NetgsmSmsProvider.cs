using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public class NetgsmSmsProvider(
    HttpClient httpClient,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<NetgsmSmsProvider> logger) : ISmsProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly NotificationOptions _options = notificationOptions.Value;
    private readonly ILogger<NetgsmSmsProvider> _logger = logger;

    public virtual async Task<SmsSendResult> SendAsync(
        SmsMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Sms.Netgsm;
        if (string.IsNullOrWhiteSpace(options.Usercode) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.MsgHeader))
        {
            return new SmsSendResult
            {
                Provider = "Netgsm",
                FailureCode = "SMS_NOT_CONFIGURED",
                FailureMessage = "Netgsm SMS provider is not configured."
            };
        }

        var recipient = NormalizeNetgsmPhoneNumber(request.ToPhoneNumber);
        if (recipient is null)
        {
            return new SmsSendResult
            {
                Provider = "Netgsm",
                FailureCode = "SMS_INVALID_PHONE",
                FailureMessage = "Phone number is not valid for Netgsm delivery."
            };
        }

        var endpoint = new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), "xmlbulkhttppost.asp");
        var payload = BuildXmlPayload(options, recipient, request.Body);
        using var content = new StringContent(payload, Encoding.UTF8, "application/xml");

        try
        {
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseBody = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Netgsm SMS request failed with status {StatusCode}. Response={Response}",
                    (int)response.StatusCode,
                    responseBody);

                return new SmsSendResult
                {
                    Provider = "Netgsm",
                    FailureCode = $"NETGSM_HTTP_{(int)response.StatusCode}",
                    FailureMessage = responseBody
                };
            }

            return new SmsSendResult
            {
                Success = true,
                Provider = "Netgsm",
                ProviderMessageId = responseBody
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Netgsm SMS send failed for {PhoneNumber}", recipient);

            return new SmsSendResult
            {
                Provider = "Netgsm",
                FailureCode = "NETGSM_SEND_FAILED",
                FailureMessage = ex.Message
            };
        }
    }

    private static string BuildXmlPayload(NetgsmSmsOptions options, string recipient, string body)
    {
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<mainbody>
  <header>
    <company>NETGSM</company>
    <usercode>{EscapeXml(options.Usercode)}</usercode>
    <password>{EscapeXml(options.Password)}</password>
    <type>1:n</type>
    <msgheader>{EscapeXml(options.MsgHeader)}</msgheader>
    <encoding>{EscapeXml(options.DefaultEncoding)}</encoding>
  </header>
  <body>
    <msg><![CDATA[{body}]]></msg>
    <no>{recipient}</no>
  </body>
</mainbody>
""";
    }

    private static string? NormalizeNetgsmPhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
        {
            return "90" + digits;
        }

        if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
        {
            return "90" + digits[1..];
        }

        if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal))
        {
            return digits;
        }

        return null;
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
