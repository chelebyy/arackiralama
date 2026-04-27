using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;
using RentACar.API.Contracts.BackgroundJobs;
using RentACar.API.Controllers;
using RentACar.Core.Entities;
using RentACar.Core.Constants;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using Xunit;
using Microsoft.AspNetCore.Http;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminBackgroundJobsControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminBackgroundJobsControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetFailed_WithInvalidPage_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.GetFailed(page: 0, cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFailed_WithInvalidPageSize_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.GetFailed(pageSize: 0, cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFailed_WithTooLargePageSize_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.GetFailed(pageSize: 201, cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFailed_ReturnsFailedJobs()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.NotificationEmailSend,
            Status = BackgroundJobStatus.Failed,
            Payload = "{}",
            ScheduledAt = DateTime.UtcNow,
            LastError = "SMTP timeout"
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.GetFailed(cancellationToken: CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<FailedBackgroundJobsResponse>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Requeue_WhenJobNotFound_ReturnsNotFound()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.Requeue(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Requeue_WhenJobNotFailed_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.NotificationEmailSend,
            Status = BackgroundJobStatus.Pending,
            Payload = "{}",
            ScheduledAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var jobId = dbContext.BackgroundJobs.Single().Id;

        var result = await controller.Requeue(jobId, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Requeue_WhenJobFailed_ResetsToPending()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.NotificationEmailSend,
            Status = BackgroundJobStatus.Failed,
            Payload = "{}",
            ScheduledAt = DateTime.UtcNow,
            RetryCount = 2,
            LastError = "Error",
            FailedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var jobId = dbContext.BackgroundJobs.Single().Id;

        var result = await controller.Requeue(jobId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        dynamic response = okResult.Value!;
        ((bool)response.Success).Should().BeTrue();

        var job = dbContext.BackgroundJobs.Single();
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.RetryCount.Should().Be(0);
        job.LastError.Should().BeNull();
        job.FailedAt.Should().BeNull();
    }

    private static AdminBackgroundJobsController CreateController(RentACarDbContext dbContext)
    {
        return new AdminBackgroundJobsController(dbContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
