using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Configuration;
using RentACar.API.Controllers;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public class AuthEndpointSecurityConventionsTests
{
    [Fact]
    public void AddAdminAuthorization_ConfiguresExpectedRolePolicies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        InvokePrivateServiceRegistration(nameof(ServiceCollectionExtensions), "AddAdminAuthorization", services);
        using var provider = services.BuildServiceProvider();
        var authorizationOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        // Assert
        var adminPolicy = authorizationOptions.GetPolicy(AuthPolicyNames.AdminOnly);
        var customerPolicy = authorizationOptions.GetPolicy(AuthPolicyNames.CustomerOnly);
        var superAdminPolicy = authorizationOptions.GetPolicy(AuthPolicyNames.SuperAdminOnly);

        adminPolicy.Should().NotBeNull();
        adminPolicy!.Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles
            .Should().BeEquivalentTo([AuthRoleNames.Admin, AuthRoleNames.SuperAdmin]);

        customerPolicy.Should().NotBeNull();
        customerPolicy!.Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles
            .Should().BeEquivalentTo([AuthRoleNames.Customer]);

        superAdminPolicy.Should().NotBeNull();
        superAdminPolicy!.Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles
            .Should().BeEquivalentTo([AuthRoleNames.SuperAdmin]);
    }

    [Fact]
    public void AddJwtAuthentication_EnablesLifetimeValidationAndChallengeHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "ThisIsAVeryLongSecretKeyForTesting123456789!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        // Act
        InvokePrivateServiceRegistration(
            nameof(ServiceCollectionExtensions),
            "AddJwtAuthentication",
            services,
            configuration,
            new FakeHostEnvironment());

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be("TestIssuer");
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("TestAudience");
        jwtOptions.Events.Should().NotBeNull();
        jwtOptions.Events.OnChallenge.Should().NotBeNull();
        jwtOptions.Events.OnTokenValidated.Should().NotBeNull();
    }

    [Fact]
    public void AddApiRateLimiting_ConfiguresGlobalLimiterAndRejectionHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        InvokePrivateServiceRegistration(nameof(ServiceCollectionExtensions), "AddApiRateLimiting", services);
        using var provider = services.BuildServiceProvider();
        var rateLimiterOptions = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Assert
        rateLimiterOptions.GlobalLimiter.Should().NotBeNull();
        rateLimiterOptions.OnRejected.Should().NotBeNull();
    }

    [Fact]
    public void AdminAuthController_ProtectedEndpoints_RequireAdminPolicyAndStandardRateLimit()
    {
        AssertEndpointSecurity(
            typeof(AdminAuthController),
            nameof(AdminAuthController.Me),
            AuthPolicyNames.AdminOnly,
            RateLimitPolicyNames.Standard);

        AssertEndpointSecurity(
            typeof(AdminAuthController),
            nameof(AdminAuthController.Logout),
            AuthPolicyNames.AdminOnly,
            RateLimitPolicyNames.Standard);
    }

    [Fact]
    public void CustomerAuthController_ProtectedEndpoints_RequireCustomerPolicyAndStandardRateLimit()
    {
        AssertEndpointSecurity(
            typeof(CustomerAuthController),
            nameof(CustomerAuthController.Me),
            AuthPolicyNames.CustomerOnly,
            RateLimitPolicyNames.Standard);

        AssertEndpointSecurity(
            typeof(CustomerAuthController),
            nameof(CustomerAuthController.UpdateProfile),
            AuthPolicyNames.CustomerOnly,
            RateLimitPolicyNames.Standard);

        AssertEndpointSecurity(
            typeof(CustomerAuthController),
            nameof(CustomerAuthController.Logout),
            AuthPolicyNames.CustomerOnly,
            RateLimitPolicyNames.Standard);
    }

    [Fact]
    public void AuthControllers_AnonymousLoginAndRefreshEndpoints_UseStrictRateLimiting()
    {
        AssertAnonymousEndpointRateLimit(typeof(AdminAuthController), nameof(AdminAuthController.Login), RateLimitPolicyNames.Strict);
        AssertAnonymousEndpointRateLimit(typeof(AdminAuthController), nameof(AdminAuthController.Refresh), RateLimitPolicyNames.Strict);
        AssertAnonymousEndpointRateLimit(typeof(CustomerAuthController), nameof(CustomerAuthController.Login), RateLimitPolicyNames.Strict);
        AssertAnonymousEndpointRateLimit(typeof(CustomerAuthController), nameof(CustomerAuthController.Refresh), RateLimitPolicyNames.Strict);
    }

    private static void AssertEndpointSecurity(Type controllerType, string actionName, string expectedPolicy, string expectedRateLimitPolicy)
    {
        var action = controllerType.GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
        var rateLimit = action.GetCustomAttribute<EnableRateLimitingAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be(expectedPolicy);
        rateLimit.Should().NotBeNull();
        rateLimit!.PolicyName.Should().Be(expectedRateLimitPolicy);
    }

    private static void AssertAnonymousEndpointRateLimit(Type controllerType, string actionName, string expectedRateLimitPolicy)
    {
        var action = controllerType.GetMethod(actionName)!;
        action.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        action.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(expectedRateLimitPolicy);
    }

    private static void InvokePrivateServiceRegistration(string typeName, string methodName, params object[] args)
    {
        var method = typeof(ServiceCollectionExtensions).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        method.Invoke(null, args);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RentACar.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
