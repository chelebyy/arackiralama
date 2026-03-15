using RentACar.Core.Entities;

namespace RentACar.API.Services;

public interface IJwtTokenService
{
    string CreateAdminAccessToken(AdminUser adminUser, Guid sessionId, out DateTime expiresAtUtc);
    string CreateCustomerAccessToken(Customer customer, Guid sessionId, out DateTime expiresAtUtc);
    string CreateRefreshToken(out DateTime expiresAtUtc);
    string HashRefreshToken(string refreshToken);
    bool VerifyRefreshToken(string refreshToken, string refreshTokenHash);
    bool IsRefreshTokenReplay(string refreshToken, string activeRefreshTokenHash);
}
