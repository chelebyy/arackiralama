using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public class TwilioSmsProvider(
    HttpClient httpClient,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<TwilioSmsProvider> logger) : ISmsProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly NotificationOptions _options = notificationOptions.Value;
    private readonly ILogger<TwilioSmsProvider> _logger = logger;

    public virtual async Task<SmsSendResult> SendAsync(
        SmsMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Sms.Twilio;
        if (string.IsNullOrWhiteSpace(options.AccountSid) ||
            string.IsNullOrWhiteSpace(options.AuthToken) ||
            string.IsNullOrWhiteSpace(options.FromPhoneNumber))
        {
            return new SmsSendResult
            {
                Provider = "Twilio",
                FailureCode = "SMS_NOT_CONFIGURED",
                FailureMessage = "Twilio SMS provider is not configured."
            };
        }

        var recipient = NormalizeTwilioPhoneNumber(request.ToPhoneNumber);
        if (recipient is null)
        {
            return new SmsSendResult
            {
                Provider = "Twilio",
                FailureCode = "SMS_INVALID_PHONE",
                FailureMessage = "Phone number is not valid for Twilio delivery."
            };
        }

        var endpoint = $"https://api.twilio.com/2010-04-01/Accounts/{options.AccountSid}/Messages.json";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = BuildContent(options, recipient, request.Body)
        };

        ApplyBasicAuth(httpRequest, options.AccountSid, options.AuthToken);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var responseJson = JsonDocument.Parse(responseBody);
            var root = responseJson.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                return new SmsSendResult
                {
                    Provider = "Twilio",
                    FailureCode = root.TryGetProperty("code", out var code) ? code.ToString() : $"TWILIO_HTTP_{(int)response.StatusCode}",
                    FailureMessage = root.TryGetProperty("message", out var message) ? message.GetString() : responseBody
                };
            }

            return new SmsSendResult
            {
                Success = true,
                Provider = "Twilio",
                ProviderMessageId = root.TryGetProperty("sid", out var sid) ? sid.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio SMS send failed for {PhoneNumber}", recipient);

            return new SmsSendResult
            {
                Provider = "Twilio",
                FailureCode = "TWILIO_SEND_FAILED",
                FailureMessage = ex.Message
            };
        }
    }

    private static FormUrlEncodedContent BuildContent(TwilioSmsOptions options, string recipient, string body)
    {
        var values = new Dictionary<string, string>
        {
            ["To"] = recipient,
            ["From"] = options.FromPhoneNumber,
            ["Body"] = body
        };

        if (!string.IsNullOrWhiteSpace(options.StatusCallbackUrl))
        {
            values["StatusCallback"] = options.StatusCallbackUrl;
        }

        return new FormUrlEncodedContent(values);
    }

    private static void ApplyBasicAuth(HttpRequestMessage request, string accountSid, string authToken)
    {
        var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }

    private static string? NormalizeTwilioPhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        if (phoneNumber.TrimStart().StartsWith('+'))
        {
            return "+" + digits;
        }

        if (digits.Length == 10)
        {
            return "+90" + digits;
        }

        if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
        {
            return "+90" + digits[1..];
        }

        if (digits.Length >= 11)
        {
            return "+" + digits;
        }

        return null;
    }
}
