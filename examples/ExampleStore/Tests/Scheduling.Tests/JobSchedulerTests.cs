using Kernel.Common;
using Kernel.Json;
using Kernel.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Scheduling.Configuration;
using Scheduling.Contracts.Constants;
using Scheduling.Contracts.Events;
using Scheduling.Cron;
using Scheduling.Entities;
using Scheduling.Handlers;
using Scheduling.Handlers.BuiltIn;
using Scheduling.Persistence;
using Scheduling.Services;
using Xunit;

namespace Scheduling.Tests;

public class JobSchedulerTests
{
    private readonly SchedulingModuleDbContext _dbContext;
    private readonly JobLockService _lockService;
    private readonly CronParser _cronParser;
    private readonly JobHandlerRegistry _registry;
    private readonly SchedulingSettings _settings;
    private readonly FakeEventBus _eventBus;
    private readonly FakeScopeContext _scopeContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly JobScheduler _scheduler;

    public class FailingJobHandler : IScheduledJobHandler
    {
        public Task<JobHandlerResult> HandleAsync(JobExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(JobHandlerResult.Failure("Intended failure for testing."));
        }
    }

    public JobSchedulerTests()
    {
        var options = new DbContextOptionsBuilder<SchedulingModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SchedulingModuleDbContext(options);
        _lockService = new JobLockService(_dbContext, NullLogger<JobLockService>.Instance);
        _cronParser = new CronParser();
        _registry = new JobHandlerRegistry();
        _registry.Register<NoOpJobHandler>();
        _registry.Register<FailingJobHandler>();

        _settings = new SchedulingSettings(new JsonSerializer());
        _eventBus = new FakeEventBus();
        _scopeContext = new FakeScopeContext().AsAuthenticated();

        var services = new ServiceCollection();
        services.AddTransient<NoOpJobHandler>();
        services.AddTransient<FailingJobHandler>();
        _serviceProvider = services.BuildServiceProvider();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _scheduler = new JobScheduler(
            _dbContext,
            _lockService,
            _cronParser,
            _registry,
            _settings,
            _eventBus,
            _scopeContext,
            scopeFactory,
            NullLogger<JobScheduler>.Instance,
            _serviceProvider);
    }

    [Fact]
    public async Task CreateJobAsync_RecurringCron_CreatesJobAndPublishesEvent()
    {
        var result = await _scheduler.CreateJobAsync(
            name: "Nightly Report",
            handlerType: typeof(NoOpJobHandler).FullName!,
            cronExpression: "0 2 * * *",
            description: "Generates nightly metrics");

        Assert.True(result.IsSuccess);
        var job = result.Value!;
        Assert.Equal("Nightly Report", job.Name);
        Assert.Equal(JobStatus.Enabled, job.Status);
        Assert.False(job.IsOneShot);
        Assert.NotNull(job.NextRunAt);

        Assert.True(_eventBus.HasPublished<JobCreatedEvent>(e => e.JobId == job.Id && e.Name == "Nightly Report"));
    }

    [Fact]
    public async Task CreateJobAsync_OneShotDelayed_CreatesOneShotJob()
    {
        var targetTime = DateTime.UtcNow.AddHours(2);
        var result = await _scheduler.CreateJobAsync(
            name: "One-off Task",
            handlerType: typeof(NoOpJobHandler).FullName!,
            scheduledAt: targetTime);

        Assert.True(result.IsSuccess);
        var job = result.Value!;
        Assert.True(job.IsOneShot);
        Assert.Equal(targetTime, job.ScheduledAt);
        Assert.Equal(targetTime, job.NextRunAt);
    }

    [Fact]
    public async Task CreateJobAsync_DuplicateName_Fails()
    {
        await _scheduler.CreateJobAsync("Duplicate Name", typeof(NoOpJobHandler).FullName!, cronExpression: "0 0 * * *");
        var second = await _scheduler.CreateJobAsync("Duplicate Name", typeof(NoOpJobHandler).FullName!, cronExpression: "0 0 * * *");

        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task CreateJobAsync_InvalidCron_Fails()
    {
        var result = await _scheduler.CreateJobAsync("Bad Cron Job", typeof(NoOpJobHandler).FullName!, cronExpression: "not-a-cron");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateJobAsync_UpdatesFieldsAndRecalculatesNextRun()
    {
        var created = (await _scheduler.CreateJobAsync("Original Job", typeof(NoOpJobHandler).FullName!, cronExpression: "0 1 * * *")).Value!;

        var updated = await _scheduler.UpdateJobAsync(
            created.Id,
            name: "Updated Job",
            description: "New description",
            cronExpression: "0 5 * * *",
            priority: 50);

        Assert.True(updated.IsSuccess);
        Assert.Equal("Updated Job", updated.Value!.Name);
        Assert.Equal("New description", updated.Value!.Description);
        Assert.Equal("0 5 * * *", updated.Value!.CronExpression);
        Assert.Equal(50, updated.Value!.Priority);
    }

    [Fact]
    public async Task DeleteJobAsync_SoftDeletesJob()
    {
        var created = (await _scheduler.CreateJobAsync("To Delete", typeof(NoOpJobHandler).FullName!, cronExpression: "0 0 * * *")).Value!;

        var result = await _scheduler.DeleteJobAsync(created.Id);
        Assert.True(result.IsSuccess);

        var getResult = await _scheduler.GetJobAsync(created.Id);
        Assert.True(getResult.IsFailure);
    }

    [Fact]
    public async Task EnableAndDisableJob_TogglesStatus()
    {
        var created = (await _scheduler.CreateJobAsync("Toggle Job", typeof(NoOpJobHandler).FullName!, cronExpression: "0 0 * * *")).Value!;

        await _scheduler.DisableJobAsync(created.Id);
        var disabled = (await _scheduler.GetJobAsync(created.Id)).Value!;
        Assert.Equal(JobStatus.Disabled, disabled.Status);

        await _scheduler.EnableJobAsync(created.Id);
        var enabled = (await _scheduler.GetJobAsync(created.Id)).Value!;
        Assert.Equal(JobStatus.Enabled, enabled.Status);
    }

    [Fact]
    public async Task TriggerJobAsync_ExecutesImmediately_RecordsExecutionAndPublishesEvents()
    {
        var created = (await _scheduler.CreateJobAsync("Manual Trigger Job", typeof(NoOpJobHandler).FullName!, cronExpression: "0 0 * * *")).Value!;

        var executionResult = await _scheduler.TriggerJobAsync(created.Id);

        Assert.True(executionResult.IsSuccess);
        var execution = executionResult.Value!;
        Assert.Equal(ExecutionStatus.Succeeded, execution.Status);
        Assert.True(execution.IsManualTrigger);
        Assert.NotNull(execution.DurationMs);

        // Verify events
        Assert.True(_eventBus.HasPublished<JobTriggeredManuallyEvent>(e => e.JobId == created.Id));
        Assert.True(_eventBus.HasPublished<JobCompletedEvent>(e => e.JobId == created.Id));

        // Verify job stats
        var job = (await _scheduler.GetJobAsync(created.Id)).Value!;
        Assert.Equal(1, job.TotalExecutions);
        Assert.Equal(0, job.ConsecutiveFailures);
        Assert.Equal(ExecutionStatus.Succeeded, job.LastRunStatus);
    }

    [Fact]
    public async Task ExecuteJobAsync_WhenHandlerFails_IncrementsFailures_AndAutoDisablesAtLimit()
    {
        var created = (await _scheduler.CreateJobAsync(
            "Failing Job",
            typeof(FailingJobHandler).FullName!,
            cronExpression: "0 0 * * *",
            maxConsecutiveFailures: 2)).Value!;

        // Attempt 1
        await _scheduler.ExecuteJobAsync(created.Id, isManualTrigger: false);
        var jobAfter1 = (await _scheduler.GetJobAsync(created.Id)).Value!;
        Assert.Equal(1, jobAfter1.ConsecutiveFailures);
        Assert.Equal(JobStatus.Enabled, jobAfter1.Status);

        // Attempt 2 -> should hit limit of 2 and auto-disable
        await _scheduler.ExecuteJobAsync(created.Id, isManualTrigger: false);
        var jobAfter2 = (await _scheduler.GetJobAsync(created.Id)).Value!;
        Assert.Equal(2, jobAfter2.ConsecutiveFailures);
        Assert.Equal(JobStatus.Disabled, jobAfter2.Status);

        Assert.True(_eventBus.HasPublished<JobFailedEvent>(e => e.JobId == created.Id));
    }

    [Fact]
    public async Task GetNextOccurrencesAsync_ReturnsPreviewDates()
    {
        var created = (await _scheduler.CreateJobAsync("Preview Job", typeof(NoOpJobHandler).FullName!, cronExpression: "0 12 * * *")).Value!;

        var occurrences = await _scheduler.GetNextOccurrencesAsync(created.Id, 3);
        Assert.True(occurrences.IsSuccess);
        Assert.Equal(3, occurrences.Value!.Count);
    }
}
