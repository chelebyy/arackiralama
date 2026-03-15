namespace RentACar.API.Configuration;

public static class AuthPolicyNames
{
    public const string GuestOnly = "GuestOnly";
    public const string CustomerOnly = "CustomerOnly";
    public const string AdminOnly = "AdminOnly";
    public const string SuperAdminOnly = "SuperAdminOnly";
}
