using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Security;

namespace RentACar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection") ??
            "Host=localhost;Port=5432;Database=rentacar;Username=postgres;Password=postgres";

        services.AddDbContext<RentACarDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(RentACarDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<RentACarDbContext>());

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        services.AddSingleton(new DatabaseSettings(connectionString));

        return services;
    }
}

public sealed record DatabaseSettings(string ConnectionString);
