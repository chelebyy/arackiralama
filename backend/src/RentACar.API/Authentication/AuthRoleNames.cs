namespace RentACar.API.Authentication;

public static class AuthRoleNames
{
    public const string Guest = "Guest";
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static bool IsAdminRole(string role) =>
        TryNormalizeAdminRole(role, out _);

    public static bool TryNormalizeAdminRole(string role, out string normalizedRole)
    {
        normalizedRole = string.Empty;

        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var candidateRole = role.Trim();

        if (candidateRole == Admin)
        {
            normalizedRole = Admin;
            return true;
        }

        if (candidateRole == SuperAdmin)
        {
            normalizedRole = SuperAdmin;
            return true;
        }

        return false;
    }
}
