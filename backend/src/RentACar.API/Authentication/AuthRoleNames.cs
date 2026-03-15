namespace RentACar.API.Authentication;

public static class AuthRoleNames
{
    public const string Guest = "Guest";
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    private static readonly HashSet<string> AdminRoles =
    [
        Admin,
        SuperAdmin
    ];

    public static bool IsAdminRole(string role) =>
        !string.IsNullOrWhiteSpace(role) && AdminRoles.Contains(role);
}
