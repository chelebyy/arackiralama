using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using RentACar.API.Configuration;
using RentACar.API.Controllers;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class RbacPolicyMatrixTests
{
    [Theory]
    [InlineData(typeof(AdminAuthController), nameof(AdminAuthController.Me))]
    [InlineData(typeof(AdminAuthController), nameof(AdminAuthController.Logout))]
    [InlineData(typeof(AdminSecurityController), nameof(AdminSecurityController.Ping))]
    public void AdminProtectedEndpoints_RequireAdminOnlyPolicy(Type controllerType, string actionName)
    {
        var policy = GetEffectivePolicy(controllerType, actionName);

        policy.Should().Be(AuthPolicyNames.AdminOnly);
        HasAllowAnonymous(controllerType, actionName).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(CustomerAuthController), nameof(CustomerAuthController.Me))]
    [InlineData(typeof(CustomerAuthController), nameof(CustomerAuthController.Logout))]
    public void CustomerProtectedEndpoints_RequireCustomerOnlyPolicy(Type controllerType, string actionName)
    {
        var policy = GetEffectivePolicy(controllerType, actionName);

        policy.Should().Be(AuthPolicyNames.CustomerOnly);
        HasAllowAnonymous(controllerType, actionName).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.GetAll))]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.Create))]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.UpdateRole))]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.Activate))]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.Deactivate))]
    [InlineData(typeof(AdminUsersController), nameof(AdminUsersController.InitiatePasswordReset))]
    public void SuperAdminProtectedEndpoints_RequireSuperAdminOnlyPolicy(Type controllerType, string actionName)
    {
        var policy = GetEffectivePolicy(controllerType, actionName);

        policy.Should().Be(AuthPolicyNames.SuperAdminOnly);
        HasAllowAnonymous(controllerType, actionName).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(AdminAuthController), nameof(AdminAuthController.Login))]
    [InlineData(typeof(AdminAuthController), nameof(AdminAuthController.Refresh))]
    [InlineData(typeof(CustomerAuthController), nameof(CustomerAuthController.Register))]
    [InlineData(typeof(CustomerAuthController), nameof(CustomerAuthController.Login))]
    [InlineData(typeof(CustomerAuthController), nameof(CustomerAuthController.Refresh))]
    [InlineData(typeof(PasswordResetController), nameof(PasswordResetController.RequestReset))]
    [InlineData(typeof(PasswordResetController), nameof(PasswordResetController.Confirm))]
    [InlineData(typeof(HealthController), nameof(HealthController.Get))]
    public void GuestAccessibleEndpoints_AreMarkedAllowAnonymous(Type controllerType, string actionName)
    {
        HasAllowAnonymous(controllerType, actionName).Should().BeTrue();
    }

    private static string? GetEffectivePolicy(Type controllerType, string actionName)
    {
        var action = GetActionMethod(controllerType, actionName);

        var actionPolicy = action
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .FirstOrDefault(policy => !string.IsNullOrWhiteSpace(policy));

        if (!string.IsNullOrWhiteSpace(actionPolicy))
        {
            return actionPolicy;
        }

        return controllerType
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .FirstOrDefault(policy => !string.IsNullOrWhiteSpace(policy));
    }

    private static bool HasAllowAnonymous(Type controllerType, string actionName)
    {
        var action = GetActionMethod(controllerType, actionName);

        return action.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
               || controllerType.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
    }

    private static MethodInfo GetActionMethod(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(
            actionName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        method.Should().NotBeNull($"{controllerType.Name}.{actionName} action must exist for RBAC matrix coverage.");

        return method!;
    }
}
