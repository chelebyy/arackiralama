namespace RentACar.API.Contracts.Fleet;

public static class VehicleCatalogueValidation
{
    public static readonly string[] EquipmentCodes = ["airConditioning", "bluetooth", "navigation", "parkingSensors", "rearCamera", "cruiseControl", "childSeatAnchors"];

    public static string? Validate(string? transmission, string? fuelType, int? seats, int? luggage,
        string? bodyType, int? doors, string? engine, int? powerHp, string[]? equipment)
    {
        if (transmission is not null && transmission is not ("manual" or "automatic" or "semiAutomatic"))
            return "Gecersiz vites turu.";
        if (fuelType is not null && fuelType is not ("gasoline" or "diesel" or "hybrid" or "electric" or "lpg"))
            return "Gecersiz yakit turu.";
        if (seats is < 1 or > 20 || luggage is < 0 or > 20 || doors is < 1 or > 6 || powerHp is < 1 or > 2000)
            return "Koltuk, bagaj, kapi veya guc degeri gecersiz.";
        if (bodyType is not null && bodyType is not ("sedan" or "hatchback" or "suv" or "estate" or "van" or "coupe" or "convertible"))
            return "Gecersiz kasa turu.";
        if (engine?.Length > 80 || equipment?.Length > EquipmentCodes.Length ||
            equipment?.Any(value => !EquipmentCodes.Contains(value, StringComparer.Ordinal)) == true ||
            equipment?.Distinct(StringComparer.Ordinal).Count() != equipment?.Length)
            return "Gecersiz motor veya donanim bilgisi.";
        return null;
    }
}

public sealed record UpdateVehiclePhotosRequest(string[] PhotoUrls);
