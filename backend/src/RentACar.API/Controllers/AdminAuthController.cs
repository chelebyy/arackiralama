using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Auth;
using RentACar.API.Services;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/auth")]
public sealed class AdminAuthController(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequestResponse("Email ve parola zorunludur.");
        }

        var email = request.Email.Trim();
        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        if (adminUser is null || !adminUser.IsActive)
        {
            return UnauthorizedResponse();
        }

        var isPasswordValid = passwordHasher.VerifyPassword(request.Password, adminUser.PasswordHash);
        if (!isPasswordValid)
        {
            return UnauthorizedResponse();
        }

        var accessToken = jwtTokenService.CreateAdminAccessToken(adminUser, out var expiresAtUtc);
        var response = new AdminLoginResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expiresAtUtc,
            Role: adminUser.Role,
            FullName: adminUser.FullName,
            Email: adminUser.Email);

        return OkResponse(response, "Giriþ baþarýlý.");
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicyNames.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public IActionResult Me()
    {
        return OkResponse(new
        {
            id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
            role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicyNames.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicyNames.Standard)]
    public IActionResult Logout()
    {
        return OkResponse(new { success = true }, "Çýkýþ baþarýlý.");
    }
}
