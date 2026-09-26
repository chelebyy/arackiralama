using System.Globalization;
using System.Text.Json;

namespace RentACar.Infrastructure.Services.Payments;

internal static class PaymentThreeDsCallbackVerifier
{
    internal static PaymentThreeDsVerificationResult Verify(
        string providerIntentId,
        string bankResponse,
        string secret,
        string failureCode,
        string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(providerIntentId) || string.IsNullOrWhiteSpace(bankResponse) || string.IsNullOrWhiteSpace(secret))
        {
            return Failed(failureCode, failureMessage);
        }

        try
        {
            using var document = JsonDocument.Parse(bankResponse);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failed(failureCode, failureMessage);
            }

            var responseIntentId = TryGetString(root, "providerIntentId")
                ?? TryGetString(root, "provider_intent_id")
                ?? TryGetString(root, "paymentConversationId");
            var status = TryGetString(root, "status");
            var transactionId = TryGetString(root, "transactionId")
                ?? TryGetString(root, "transaction_id")
                ?? TryGetString(root, "paymentId");
            var timestamp = TryGetString(root, "timestamp");
            var signature = TryGetString(root, "signature");

            if (string.IsNullOrWhiteSpace(responseIntentId)
                || !string.Equals(responseIntentId, providerIntentId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(status)
                || string.IsNullOrWhiteSpace(transactionId)
                || string.IsNullOrWhiteSpace(timestamp)
                || string.IsNullOrWhiteSpace(signature)
                || !IsFreshTimestamp(timestamp))
            {
                return Failed(failureCode, failureMessage);
            }

            var canonicalPayload = CreateCanonicalPayload(responseIntentId, status, transactionId, timestamp);
            if (!PaymentSignatureHelper.IsValidSignature(canonicalPayload, secret, signature))
            {
                return Failed(failureCode, failureMessage);
            }

            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return new PaymentThreeDsVerificationResult(true, transactionId, null, null);
            }

            return Failed(failureCode, failureMessage);
        }
        catch (JsonException)
        {
            return Failed(failureCode, failureMessage);
        }
    }

    internal static string CreateCanonicalPayload(string providerIntentId, string status, string transactionId, string timestamp) =>
        $"{providerIntentId}.{status}.{transactionId}.{timestamp}";

    private static bool IsFreshTimestamp(string timestamp)
    {
        DateTimeOffset timestampUtc;
        if (long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            try
            {
                timestampUtc = DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
        else if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestampUtc))
        {
            return false;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        return nowUtc - timestampUtc <= TimeSpan.FromMinutes(5)
            && timestampUtc - nowUtc <= TimeSpan.FromSeconds(30);
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static PaymentThreeDsVerificationResult Failed(string failureCode, string failureMessage) =>
        new(false, null, failureCode, failureMessage);
}

internal sealed record PaymentThreeDsVerificationResult(
    bool IsSucceeded,
    string? TransactionId,
    string? FailureCode,
    string? FailureMessage);
