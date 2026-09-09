using Kernel.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Scheduling.Configuration;
using Scheduling.Contracts.Constants;
using Scheduling.Entities;
using Scheduling.Persistence;
using Scheduling.Workers;
using System.Reflection;
using Xunit;

namespace Scheduling.Tests;

public class ExecutionCleanupWorkerTests
{
    private readonly SchedulingModuleDbContext _dbContext;
    private readonly SchedulingSettings _settings;
    private readonly ExecutionCleanupWorker _worker;
    private readonly IServiceProvider _serviceProvider;

    public ExecutionCleanupWorkerTests()
    {
        var options = new DbContextOptionsBuilder<SchedulingModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SchedulingModuleDbContext(options);
        _settings = new SchedulingSettings(new JsonSerializer())
        {
            AutoSave = false
        };

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        _serviceProvider = services.BuildServiceProvider();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _worker = new ExecutionCleanupWorker(scopeFactory, _settings, NullLogger<ExecutionCleanupWorker>.Instance);
    }

    [Fact]
    public async Task CleanupWorker_PurgesOldExecutionsAndExpiredLocks()
    {
        var jobId = DefaultId.NewUlid();

        // 1. Old execution (35 days ago, completed 35 days ago)
        var oldExecution = new JobExecution
        {
            Id = DefaultId.NewUlid(),
            JobId = jobId,
            Status = ExecutionStatus.Succeeded,
            HandlerType = "TestHandler",
            StartedAt = DateTime.UtcNow.AddDays(-35),
            CompletedAt = DateTime.UtcNow.AddDays(-35)
        };

        // 2. Recent execution (2 days ago)
        var recentExecution = new JobExecution
        {
            Id = DefaultId.NewUlid(),
            JobId = jobId,
            Status = ExecutionStatus.Succeeded,
            HandlerType = "TestHandler",
            StartedAt = DateTime.UtcNow.AddDays(-2),
            CompletedAt = DateTime.UtcNow.AddDays(-2)
        };

        // 3. Stale lock (expired 10 minutes ago)
        var staleLock = new JobLock
        {
            JobId = DefaultId.NewUlid(),
            LockedByInstance = "inst-old",
            AcquiredAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // 4. Active lock (expires in 10 minutes)
        var activeLock = new JobLock
        {
            JobId = DefaultId.NewUlid(),
            LockedByInstance = "inst-active",
            AcquiredAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _dbContext.Executions.AddRange(oldExecution, recentExecution);
        _dbContext.Locks.AddRange(staleLock, activeLock);
        await _dbContext.SaveChangesAsync();

        // Invoke protected ExecuteJobAsync via reflection
        var method = typeof(ExecutionCleanupWorker).GetMethod("ExecuteJobAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method.Invoke(_worker, new object[] { _serviceProvider, CancellationToken.None })!;
        await task;

        // Verify old execution was deleted, recent kept
        var remainingExecutions = await _dbContext.Executions.ToListAsync();
        Assert.Single(remainingExecutions);
        Assert.Equal(recentExecution.Id, remainingExecutions[0].Id);

        // Verify stale lock deleted, active kept
        var remainingLocks = await _dbContext.Locks.ToListAsync();
        Assert.Single(remainingLocks);
        Assert.Equal(activeLock.JobId, remainingLocks[0].JobId);
    }
}
