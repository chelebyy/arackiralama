using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Infrastructure.Security;
using RentACar.Infrastructure.Services;
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

        // Add Redis
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            redisConnectionString = configuration["Redis:ConnectionString"];
        }

        redisConnectionString ??= "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddSingleton(new DatabaseSettings(connectionString));

        return services;
    }
}

public sealed record DatabaseSettings(string ConnectionString);
