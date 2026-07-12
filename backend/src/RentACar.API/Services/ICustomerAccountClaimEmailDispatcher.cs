namespace RentACar.API.Services;

public interface ICustomerAccountClaimEmailDispatcher
{
    Task DispatchAsync(
        string destinationEmail,
        string rawToken,
        DateTime expiresAtUtc,
        string locale,
        CancellationToken cancellationToken = default);
}