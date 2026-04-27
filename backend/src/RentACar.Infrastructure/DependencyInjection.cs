using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Infrastructure.Security;
using RentACar.Infrastructure.Services;
using RentACar.Infrastructure.Services.Notifications;
using StackExchange.Redis;

namespace RentACar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' must be configured.");
        }

        services.AddDbContext<RentACarDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(RentACarDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<RentACarDbContext>());

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IVehicleGroupRepository, VehicleGroupRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IReservationHoldService, RedisReservationHoldService>();
        services.AddScoped<NetgsmSmsProvider>(serviceProvider => new NetgsmSmsProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>(),
            serviceProvider.GetRequiredService<ILogger<NetgsmSmsProvider>>()));
        services.AddScoped<TwilioSmsProvider>(serviceProvider => new TwilioSmsProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>(),
            serviceProvider.GetRequiredService<ILogger<TwilioSmsProvider>>()));
        services.AddScoped<INotificationBackgroundJobProcessor, NotificationBackgroundJobProcessor>();
        services.AddScoped<INotificationQueueService, NotificationQueueService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<ISmsProvider, ConfiguredSmsProvider>();
        services.AddScoped<IEmailProvider, SmtpEmailProvider>();

        // Add Redis
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            redisConnectionString = configuration["Redis:ConnectionString"];
        }

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            redisConnectionString = "localhost:6379";
        }

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddSingleton(new DatabaseSettings(connectionString));

        return services;
    }
}

public sealed record DatabaseSettings(string ConnectionString);
