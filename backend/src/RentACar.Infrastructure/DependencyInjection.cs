using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Core.Entities;
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
        services.AddScoped<IRepository<Vehicle>, VehicleRepository>();
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IRepository<Office>, OfficeRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IRepository<Customer>, CustomerRepository>();
        services.AddScoped<IReservationHoldService, RedisReservationHoldService>();
        services.AddHttpClient<NetgsmSmsProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
        services.AddHttpClient<TwilioSmsProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
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
