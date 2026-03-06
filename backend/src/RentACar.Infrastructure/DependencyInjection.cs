using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Infrastructure.Security;

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

        services.AddSingleton(new DatabaseSettings(connectionString));

        return services;
    }
}

public sealed record DatabaseSettings(string ConnectionString);
