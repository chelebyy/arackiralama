using FluentAssertions;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Integration.Data;

public class DbContextTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _factory;

    public DbContextTests(TestDbContextFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CanCreateInMemoryDatabase()
    {
        // Act
        using var context = _factory.CreateContext();

        // Assert
        context.Should().NotBeNull();
        context.Database.Should().NotBeNull();
    }

    [Fact]
    public async Task CanConnectToDatabase()
    {
        // Arrange
        using var context = _factory.CreateContext();

        // Act
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
    }
}
