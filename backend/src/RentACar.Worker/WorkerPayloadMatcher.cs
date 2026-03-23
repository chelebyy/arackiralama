using System.Text.Json;

namespace RentACar.Worker;

internal static class WorkerPayloadMatcher
{
    public static bool HasReservationId(string? payload, Guid reservationId)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("ReservationId", out var reservationIdProperty) &&
                   reservationIdProperty.ValueKind == JsonValueKind.String &&
                   string.Equals(reservationIdProperty.GetString(), reservationId.ToString(), StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
