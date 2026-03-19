using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Contracts;
using RentACar.API.Options;
using RentACar.API.Services;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure;
using RentACar.Infrastructure.Services.Payments;

namespace RentACar.API.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddEndpointsApiExplorer();
        services.AddControllers();
        services.AddHealthChecks();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<RefreshTokenCookieSettings>(configuration.GetSection(RefreshTokenCookieSettings.SectionName));
        services.AddInfrastructure(configuration);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenCookieService, RefreshTokenCookieService>();
        services.AddScoped<IPasswordResetEmailDispatcher, PasswordResetEmailDispatcher>();
        services.AddScoped<IAccessTokenSessionValidator, AccessTokenSessionValidator>();
        services.AddScoped<IVehiclePhotoStorage, LocalVehiclePhotoStorage>();
        services.AddScoped<IFleetService, FleetService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<IPaymentService>(serviceProvider => serviceProvider.GetRequiredService<PaymentService>());
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddPaymentIntegration(configuration);
        services.AddHostedService<QueuedPaymentWebhookHostedService>();
        services.AddJwtAuthentication(configuration);
        services.AddAdminAuthorization();
        services.AddApiRateLimiting();

        return services;
    }

    private static IServiceCollection AddPaymentIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentOptions>(configuration.GetSection("Payment"));
        services.AddScoped<MockPaymentProvider>();
        services.AddScoped<IyzicoPaymentProvider>();
        services.AddScoped<IPaymentProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PaymentOptions>>().Value;
            return options.Provider.Equals("Iyzico", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<IyzicoPaymentProvider>()
                : serviceProvider.GetRequiredService<MockPaymentProvider>();
        });

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT secret must be configured with at least 32 characters.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal is null)
                        {
                            context.Fail("Yetkisiz erişim");
                            return;
                        }

                        var sessionValidator = context.HttpContext.RequestServices.GetRequiredService<IAccessTokenSessionValidator>();
                        var validationFailure = await sessionValidator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted);
                        if (validationFailure != AccessTokenSessionValidationFailure.None)
                        {
                            context.Fail("Yetkisiz erişim");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var payload = JsonSerializer.Serialize(ApiResponse<object>.Fail("Yetkisiz erişim"));
                        await context.Response.WriteAsync(payload);
                    }
                };
            });

        return services;
    }

    private static IServiceCollection AddAdminAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicyNames.GuestOnly, policy =>
                policy.RequireAssertion(context => context.User.Identity?.IsAuthenticated != true));
            options.AddPolicy(AuthPolicyNames.CustomerOnly, policy =>
                policy.RequireRole(AuthRoleNames.Customer));
            options.AddPolicy(AuthPolicyNames.AdminOnly, policy =>
                policy.RequireRole(AuthRoleNames.Admin, AuthRoleNames.SuperAdmin));
            options.AddPolicy(AuthPolicyNames.SuperAdminOnly, policy =>
                policy.RequireRole(AuthRoleNames.SuperAdmin));
        });

        return services;
    }

    private static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                CreateFixedWindowPartition(
                    partitionKey: GetUserOrIpPartitionKey(context),
                    permitLimit: 100,
                    queueLimit: 2));

            options.AddPolicy(RateLimitPolicyNames.Strict, CreateIpFixedWindowPolicy(permitLimit: 5, queueLimit: 0));
            options.AddPolicy(RateLimitPolicyNames.Payment, CreateIpFixedWindowPolicy(permitLimit: 10, queueLimit: 1));
            options.AddPolicy(RateLimitPolicyNames.Standard, CreateIpFixedWindowPolicy(permitLimit: 30, queueLimit: 2));
            options.AddPolicy(RateLimitPolicyNames.Health, CreateIpFixedWindowPolicy(permitLimit: 10, queueLimit: 0));

            options.OnRejected = async (rateLimitContext, cancellationToken) =>
            {
                rateLimitContext.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await rateLimitContext.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Rate limit exceeded. Try again later." },
                    cancellationToken);
            };
        });

        return services;
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateIpFixedWindowPolicy(int permitLimit, int queueLimit) =>
        context => CreateFixedWindowPartition(GetIpPartitionKey(context), permitLimit, queueLimit);

    private static RateLimitPartition<string> CreateFixedWindowPartition(string partitionKey, int permitLimit, int queueLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = queueLimit
            });

    private static string GetUserOrIpPartitionKey(HttpContext context)
    {
        var userName = context.User.Identity?.Name;
        return string.IsNullOrWhiteSpace(userName) ? GetIpPartitionKey(context) : userName;
    }

    private static string GetIpPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}
