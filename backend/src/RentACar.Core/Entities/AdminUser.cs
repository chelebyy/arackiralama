namespace RentACar.Core.Entities;

public class AdminUser : BaseEntity
{
    private string _email = string.Empty;

    public string Email
    {
        get => _email;
        set
        {
            _email = value?.Trim() ?? string.Empty;
            NormalizedEmail = NormalizeEmail(_email);
        }
    }

    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public int TokenVersion { get; set; }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        return email.Trim().ToUpperInvariant();
    }
}
