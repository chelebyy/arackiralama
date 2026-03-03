using Microsoft.EntityFrameworkCore;
using RentACar.Infrastructure.Data;

namespace RentACar.Tests.TestFixtures;

public class TestDbContextFactory : IDisposable
{
    private DbContextOptions<RentACarDbContext>? _options;

    public RentACarDbContext CreateContext()
    {
        _options = new DbContextOptionsBuilder<RentACarDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        return new RentACarDbContext(_options);
    }

    public void Dispose()
    {
        // InMemory database is automatically cleaned up when context is disposed
        GC.SuppressFinalize(this);
    }
}
