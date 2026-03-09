using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RentACar.Infrastructure.Data;

public sealed class RentACarDbContextFactory : IDesignTimeDbContextFactory<RentACarDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5433;Database=rentacar;Username=postgres;Password=postgres";

    public RentACarDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection")
            ?? FallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<RentACarDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(RentACarDbContext).Assembly.FullName));

        return new RentACarDbContext(optionsBuilder.Options);
    }
}
