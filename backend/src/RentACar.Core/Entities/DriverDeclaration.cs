namespace RentACar.Core.Entities;

public sealed record DriverDeclaration
{
    public int AgeAtPickup { get; init; }
    public int LicenseYearsAtPickup { get; init; }
    public bool LicenseValidThroughReturn { get; init; }
    public bool DocumentsAvailableAtPickup { get; init; }
}
