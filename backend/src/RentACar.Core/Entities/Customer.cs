namespace RentACar.Core.Entities;

public class Customer : BaseEntity
{
    private string _email = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

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
    public DateOnly? BirthDate { get; set; }
    public int LicenseYear { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public int TokenVersion { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        return email.Trim().ToUpperInvariant();
    }
}
