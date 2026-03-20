using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RentACar.API.Services;

namespace RentACar.API.Filters;

/// <summary>
/// Action filter that automatically logs admin operations to the audit log.
/// Applies to all POST, PUT, PATCH, DELETE actions in admin controllers.
/// </summary>
public class AuditLogActionFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogActionFilter> _logger;

    // Run after other filters but before the action
    public int Order => 100;

    public AuditLogActionFilter(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogActionFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controllerName = context.ActionDescriptor.RouteValues["controller"] ?? "Unknown";

        // Only apply to Admin controllers
        if (!controllerName.StartsWith("Admin"))
        {
            await next();
            return;
        }

        // Only log mutation operations (POST, PUT, PATCH, DELETE)
        var method = context.HttpContext.Request.Method;
        if (!IsMutationMethod(method))
        {
            await next();
            return;
        }

        // Get action info before execution
        var actionName = context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
        var entityType = GetEntityType(controllerName);

        // Extract entity ID from route or body
        var entityId = GetEntityId(context);
        var oldValue = string.Empty;
        var newValue = string.Empty;

        // Execute the action
        var executedContext = await next();

        // Only log if action was successful (2xx status)
        if (executedContext.Result is ObjectResult { StatusCode: >= 200 and < 300 } or StatusCodeResult { StatusCode: >= 200 and < 300 })
        {
            // Extract user info
            var userId = context.HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Serialize new value from response
            if (executedContext.Result is ObjectResult objectResult && objectResult.Value != null)
            {
                try
                {
                    newValue = JsonSerializer.Serialize(objectResult.Value, new JsonSerializerOptions
                    {
                        MaxDepth = 2,
                        WriteIndented = false
                    });
                }
                catch
                {
                    newValue = "[Unable to serialize]";
                }
            }

            // Log the action asynchronously without blocking the response
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                    await auditService.LogAsync(
                        action: $"{controllerName}.{actionName}",
                        entityType: entityType,
                        entityId: entityId,
                        userId: userId,
                        oldValue: oldValue,
                        newValue: newValue,
                        ipAddress: ipAddress,
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to log audit for {Controller}.{Action}", controllerName, actionName);
                }
            });
        }
    }

    private static bool IsMutationMethod(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetEntityType(string controllerName)
    {
        // Remove "Admin" prefix if present
        return controllerName.StartsWith("Admin")
            ? controllerName.Substring(5)
            : controllerName;
    }

    private static string GetEntityId(ActionExecutingContext context)
    {
        // Try to get ID from route
        if (context.ActionArguments.TryGetValue("id", out var idValue) && idValue != null)
        {
            return idValue.ToString() ?? "unknown";
        }

        // Try to get ID from body for POST requests
        if (context.ActionArguments.TryGetValue("request", out var request) && request != null)
        {
            var idProperty = request.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                var id = idProperty.GetValue(request);
                if (id != null)
                {
                    return id.ToString() ?? "unknown";
                }
            }
        }

        return "new";
    }
}
