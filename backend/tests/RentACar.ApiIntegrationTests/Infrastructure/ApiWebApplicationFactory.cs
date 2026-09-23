using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure;
using RentACar.Infrastructure.Data;
using StackExchange.Redis;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API pipeline with integration-test specific database, Redis, and JWT settings.
/// </summary>
public sealed class ApiWebApplicationFactory(
    PostgresFixture postgresFixture,
    RedisFixture redisFixture,
    string redisKeyPrefix,
    IReadOnlyDictionary<string, string?>? additionalConfiguration = null) : WebApplicationFactory<Program>
{
    public string ConnectionString => postgresFixture.ConnectionString;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            var configurationValues = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = postgresFixture.ConnectionString,
                ["ConnectionStrings:Redis"] = redisFixture.ConnectionString,
                ["Redis:ConnectionString"] = redisFixture.ConnectionString,
                ["Jwt:Issuer"] = TestJwtFactory.JwtIssuer,
                ["Jwt:Audience"] = TestJwtFactory.JwtAudience,
                ["Jwt:Secret"] = TestJwtFactory.JwtSecret,
                ["Database:AutoMigrateOnStartup"] = "false",
                ["Notifications:PublicFrontendBaseUrl"] = "https://rental.example.test",
                ["Payment:EnablePayments"] = "true"
            };

            if (additionalConfiguration is not null)
            {
                foreach (var pair in additionalConfiguration)
                {
                    configurationValues[pair.Key] = pair.Value;
                }
            }

            config.AddInMemoryCollection(configurationValues);
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RentACarDbContext>>();
            services.RemoveAll<RentACarDbContext>();
            services.RemoveAll<IApplicationDbContext>();
            services.RemoveAll<DatabaseSettings>();
            services.RemoveAll<IConnectionMultiplexer>();

            services.AddDbContext<RentACarDbContext>(options =>
                options.UseNpgsql(postgresFixture.ConnectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsAssembly(typeof(RentACarDbContext).Assembly.FullName)));

            services.AddScoped<IApplicationDbContext>(provider =>
                provider.GetRequiredService<RentACarDbContext>());

            services.AddSingleton(new DatabaseSettings(postgresFixture.ConnectionString));
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                new KeyPrefixedConnectionMultiplexer(redisFixture.ConnectionMultiplexer, redisKeyPrefix));
        });
    }
}
