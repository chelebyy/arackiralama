using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using RentACar.API.Authentication;
using RentACar.API.Contracts;
using RentACar.API.Filters;
using RentACar.API.Options;
using RentACar.API.Services;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure;
using RentACar.Infrastructure.Services.Notifications;
using RentACar.Infrastructure.Services.Payments;

namespace RentACar.API.Configuration;

public static class ServiceCollectionExtensions
{
    public const string ApiCorsPolicyName = "ApiCors";
    public static IServiceCollection AddApiApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOpenApi();
        services.AddEndpointsApiExplorer();
        services.AddControllers();
        services.AddHealthChecks();
        services.AddApiCors(configuration, environment);
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
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
        services.AddScoped<IReservationExtraPricingService, ReservationExtraPricingService>();
        services.AddScoped<IReservationQuoteService, ReservationQuoteService>();
        services.AddSingleton<AvailabilityCacheInvalidationSignal>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<IPaymentService>(serviceProvider => serviceProvider.GetRequiredService<PaymentService>());
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IPublicSiteSettingsService, PublicSiteSettingsService>();
        services.AddScoped<IReservationExtraOptionCatalogService, ReservationExtraOptionCatalogService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddPaymentIntegration(configuration);
        services.AddHostedService<QueuedPaymentWebhookHostedService>();
        services.AddJwtAuthentication(configuration, environment);
        services.AddAdminAuthorization();
        services.AddApiRateLimiting(configuration);
        services.AddAdminAuditLogging();

        return services;
    }

    private static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if ((allowedOrigins is null || allowedOrigins.Length == 0) && environment.IsDevelopment())
        {
            allowedOrigins =
            [
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:3001",
                "http://127.0.0.1:3001"
            ];
        }

        services.AddCors(options =>
        {
            options.AddPolicy(ApiCorsPolicyName, policy =>
            {
                if (allowedOrigins is null || allowedOrigins.Length == 0)
                {
                    return;
                }

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddAdminAuditLogging(this IServiceCollection services)
    {
        services.AddScoped<AuditLogActionFilter>();
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

    private static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        JwtSecretValidator.Validate(jwtOptions, environment);

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

    private static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        const string loadTestSessionHeaderName = "X-Session-Id";
        var allowLoadTestSessionPartition = configuration.GetValue<bool>("RateLimiting:LoadTestSessionPartition");

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                CreateFixedWindowPartition(
                    partitionKey: GetUserOrIpPartitionKey(context, loadTestSessionHeaderName, allowLoadTestSessionPartition),
                    permitLimit: 100,
                    queueLimit: 2));

            options.AddPolicy(
                RateLimitPolicyNames.Strict,
                CreateUserOrIpFixedWindowPolicy(
                    permitLimit: 5,
                    queueLimit: 0,
                    loadTestSessionHeaderName,
                    allowLoadTestSessionPartition));
            options.AddPolicy(
                RateLimitPolicyNames.Payment,
                CreateUserOrIpFixedWindowPolicy(
                    permitLimit: 10,
                    queueLimit: 1,
                    loadTestSessionHeaderName,
                    allowLoadTestSessionPartition));
            options.AddPolicy(
                RateLimitPolicyNames.Standard,
                CreateUserOrIpFixedWindowPolicy(
                    permitLimit: 30,
                    queueLimit: 2,
                    loadTestSessionHeaderName,
                    allowLoadTestSessionPartition));
            options.AddPolicy(
                RateLimitPolicyNames.Health,
                CreateUserOrIpFixedWindowPolicy(
                    permitLimit: 10,
                    queueLimit: 0,
                    loadTestSessionHeaderName,
                    allowLoadTestSessionPartition));

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

    private static Func<HttpContext, RateLimitPartition<string>> CreateUserOrIpFixedWindowPolicy(
        int permitLimit,
        int queueLimit,
        string loadTestSessionHeaderName,
        bool allowLoadTestSessionPartition) =>
        context => CreateFixedWindowPartition(
            GetUserOrIpPartitionKey(context, loadTestSessionHeaderName, allowLoadTestSessionPartition),
            permitLimit,
            queueLimit);

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

    private static string GetUserOrIpPartitionKey(
        HttpContext context,
        string loadTestSessionHeaderName,
        bool allowLoadTestSessionPartition)
    {
        if (allowLoadTestSessionPartition)
        {
            var loadTestSessionId = context.Request.Headers[loadTestSessionHeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(loadTestSessionId))
            {
                return $"load-test:{loadTestSessionId}";
            }
        }

        var userName = context.User.Identity?.Name;
        return string.IsNullOrWhiteSpace(userName) ? GetIpPartitionKey(context) : userName;
    }

    private static string GetIpPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}
