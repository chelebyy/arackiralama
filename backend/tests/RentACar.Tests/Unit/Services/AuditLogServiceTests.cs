using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Services;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class AuditLogServiceTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AuditLogServiceTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task LogAsync_WhenCalled_LogsToDatabase()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var auditLogService = new AuditLogService(dbContext);

        var userId = "admin-123";
        var ipAddress = "192.168.1.1";
        var entityId = Guid.NewGuid().ToString();

        await auditLogService.LogAsync(
            "Create",
            "Reservation",
            entityId,
            userId,
            null,
            "{\"status\":\"pending\"}",
            ipAddress,
            CancellationToken.None);

        var auditLog = await dbContext.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Create");
        auditLog.EntityType.Should().Be("Reservation");
        auditLog.EntityId.Should().Be(entityId);
        auditLog.UserId.Should().Be(userId);
        auditLog.OldValue.Should().BeNull();
        auditLog.NewValue.Should().Be("{\"status\":\"pending\"}");
        auditLog.IpAddress.Should().Be(ipAddress);
        auditLog.Details.Should().Contain("Create");
    }

    [Fact]
    public async Task LogAsync_WithOldAndNewValues_LogsBothValues()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var auditLogService = new AuditLogService(dbContext);

        var userId = "admin-456";
        var ipAddress = "10.0.0.1";
        var entityId = Guid.NewGuid().ToString();
        var oldValue = "{\"status\":\"pending\"}";
        var newValue = "{\"status\":\"confirmed\"}";

        await auditLogService.LogAsync(
            "Update",
            "Reservation",
            entityId,
            userId,
            oldValue,
            newValue,
            ipAddress,
            CancellationToken.None);

        var auditLog = await dbContext.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Update");
        auditLog.OldValue.Should().Be(oldValue);
        auditLog.NewValue.Should().Be(newValue);
    }

    [Fact]
    public async Task LogAsync_WithoutUserId_LogsWithNullUser()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var auditLogService = new AuditLogService(dbContext);

        var entityId = Guid.NewGuid().ToString();

        await auditLogService.LogAsync(
            "Delete",
            "Vehicle",
            entityId,
            null,
            "{\"plate\":\"07ABC123\"}",
            null,
            "127.0.0.1",
            CancellationToken.None);

        var auditLog = await dbContext.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.UserId.Should().BeNull();
        auditLog.Action.Should().Be("Delete");
        auditLog.EntityType.Should().Be("Vehicle");
    }
}
