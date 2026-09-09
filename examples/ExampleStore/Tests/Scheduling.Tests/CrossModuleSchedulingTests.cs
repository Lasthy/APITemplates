using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts.Dtos;
using Scheduling.Contracts.Requests;
using Scheduling.Cron;
using Scheduling.Entities;
using Scheduling.Features;
using Scheduling.Handlers;
using Scheduling.Handlers.BuiltIn;
using Scheduling.Persistence;
using Scheduling.Services;
using Xunit;

namespace Scheduling.Tests;

public class CrossModuleSchedulingTests
{
    private readonly SchedulingModuleDbContext _dbContext;
    private readonly ScheduledJob _testJob;

    public CrossModuleSchedulingTests()
    {
        var options = new DbContextOptionsBuilder<SchedulingModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SchedulingModuleDbContext(options);

        _testJob = new ScheduledJob
        {
            Id = DefaultId.NewUlid(),
            Name = "Cross Module Job",
            HandlerType = typeof(NoOpJobHandler).FullName!,
            CronExpression = "0 6 * * *",
            Status = "Enabled",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Jobs.Add(_testJob);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetJobSummaryHandler_ReturnsScheduledJobDto()
    {
        var handler = new GetJobSummaryHandler(_dbContext);
        var result = await handler.HandleAsync(new GetJobSummary(_testJob.Id));

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(_testJob.Id, dto.Id);
        Assert.Equal("Cross Module Job", dto.Name);
        Assert.Equal("0 6 * * *", dto.CronExpression);
        Assert.Equal("Enabled", dto.Status);
    }

    [Fact]
    public async Task ScheduleJobHandler_ProgrammaticallySchedulesJob()
    {
        // Mock or fake IJobScheduler
        var fakeScheduler = new FakeScheduler();
        var handler = new ScheduleJobHandler(fakeScheduler);

        var request = new ScheduleJob(
            Name: "Dynamic Backup",
            HandlerType: typeof(NoOpJobHandler).FullName!,
            CronExpression: "0 0 * * *",
            Description: "Nightly backup");

        var result = await handler.HandleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dynamic Backup", result.Value!.Name);
        Assert.Equal("0 0 * * *", result.Value!.CronExpression);
    }

    private class FakeScheduler : IJobScheduler
    {
        public Task<Kernel.Common.Result<ScheduledJobDetailDto>> CreateJobAsync(string name, string handlerType, string? cronExpression = null, DateTime? scheduledAt = null, string? payloadJson = null, string? description = null, int? timeoutSeconds = null, int? maxConsecutiveFailures = null, int priority = 100, CancellationToken ct = default)
        {
            var id = DefaultId.NewUlid();
            var dto = new ScheduledJobDetailDto(id, name, description, handlerType, cronExpression, scheduledAt, payloadJson, "Enabled", DateTime.UtcNow.AddHours(1), null, null, 0, 0, 0, timeoutSeconds, priority, false, DateTime.UtcNow);
            return Task.FromResult(Kernel.Common.Result.Success(dto));
        }

        public Task<Kernel.Common.Result<ScheduledJobDetailDto>> UpdateJobAsync(DefaultId id, string? name = null, string? description = null, string? cronExpression = null, string? payloadJson = null, int? timeoutSeconds = null, int? priority = null, int? maxConsecutiveFailures = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<bool>> DeleteJobAsync(DefaultId id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<bool>> EnableJobAsync(DefaultId id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<bool>> DisableJobAsync(DefaultId id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<JobExecutionDetailDto>> TriggerJobAsync(DefaultId id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<ScheduledJobDetailDto>> GetJobAsync(DefaultId id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.PaginatedResult<ScheduledJobDto>> GetJobsAsync(string? status = null, string? handlerType = null, int page = 1, int pageSize = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<IReadOnlyList<DateTime>>> GetNextOccurrencesAsync(DefaultId id, int count = 5, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Kernel.Common.Result<JobExecutionDetailDto>> ExecuteJobAsync(DefaultId jobId, bool isManualTrigger, CancellationToken ct = default) => throw new NotImplementedException();
        public Task HandleMissedFiresAsync(CancellationToken ct = default) => Task.CompletedTask;
        public string GetInstanceId() => "test-inst";
    }
}
