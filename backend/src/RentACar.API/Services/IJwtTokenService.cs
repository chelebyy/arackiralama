using RentACar.Core.Entities;

namespace RentACar.API.Services;

public interface IJwtTokenService
{
    string CreateAdminAccessToken(AdminUser adminUser, out DateTime expiresAtUtc);
}
