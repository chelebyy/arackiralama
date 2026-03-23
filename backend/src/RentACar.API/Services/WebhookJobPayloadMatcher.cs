using System.Text.Json;

namespace RentACar.API.Services;

internal static class WebhookJobPayloadMatcher
{
    public static bool HasProviderEventId(string payload, string providerEventId)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(providerEventId))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("ProviderEventId", out var providerEventIdElement))
            {
                return false;
            }

            return providerEventIdElement.ValueKind == JsonValueKind.String &&
                string.Equals(providerEventIdElement.GetString(), providerEventId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
