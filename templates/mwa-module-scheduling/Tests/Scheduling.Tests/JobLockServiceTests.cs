using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Scheduling.Entities;
using Scheduling.Persistence;
using Scheduling.Services;
using Xunit;

namespace Scheduling.Tests;

public class JobLockServiceTests
{
    private readonly SchedulingModuleDbContext _dbContext;
    private readonly JobLockService _lockService;

    public JobLockServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchedulingModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SchedulingModuleDbContext(options);
        _lockService = new JobLockService(_dbContext, NullLogger<JobLockService>.Instance);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenNoLock_AcquiresSuccessfully()
    {
        var jobId = DefaultId.NewUlid();
        var acquired = await _lockService.TryAcquireAsync(jobId, "instance-1", TimeSpan.FromMinutes(5));

        Assert.True(acquired);

        var lockRecord = await _dbContext.Locks.FirstOrDefaultAsync(l => l.JobId == jobId);
        Assert.NotNull(lockRecord);
        Assert.Equal("instance-1", lockRecord.LockedByInstance);
        Assert.True(lockRecord.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLockedByAnotherInstance_ReturnsFalse()
    {
        var jobId = DefaultId.NewUlid();
        await _lockService.TryAcquireAsync(jobId, "instance-1", TimeSpan.FromMinutes(5));

        var acquiredByTwo = await _lockService.TryAcquireAsync(jobId, "instance-2", TimeSpan.FromMinutes(5));
        Assert.False(acquiredByTwo);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenSameInstance_RenewsLock()
    {
        var jobId = DefaultId.NewUlid();
        await _lockService.TryAcquireAsync(jobId, "instance-1", TimeSpan.FromMinutes(1));

        var renewed = await _lockService.TryAcquireAsync(jobId, "instance-1", TimeSpan.FromMinutes(10));
        Assert.True(renewed);

        var lockRecord = await _dbContext.Locks.FirstOrDefaultAsync(l => l.JobId == jobId);
        Assert.NotNull(lockRecord);
        Assert.True(lockRecord.ExpiresAt > DateTime.UtcNow.AddMinutes(8));
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLockExpired_StealsLock()
    {
        var jobId = DefaultId.NewUlid();
        // Insert expired lock
        _dbContext.Locks.Add(new JobLock
        {
            JobId = jobId,
            LockedByInstance = "instance-1",
            AcquiredAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        });
        await _dbContext.SaveChangesAsync();

        var acquired = await _lockService.TryAcquireAsync(jobId, "instance-2", TimeSpan.FromMinutes(5));
        Assert.True(acquired);

        var lockRecord = await _dbContext.Locks.FirstOrDefaultAsync(l => l.JobId == jobId);
        Assert.NotNull(lockRecord);
        Assert.Equal("instance-2", lockRecord.LockedByInstance);
    }

    [Fact]
    public async Task ReleaseAsync_ReleasesLockForOwner_AllowsNewAcquisition()
    {
        var jobId = DefaultId.NewUlid();
        await _lockService.TryAcquireAsync(jobId, "instance-1", TimeSpan.FromMinutes(5));

        await _lockService.ReleaseAsync(jobId, "instance-1");

        var lockRecord = await _dbContext.Locks.FirstOrDefaultAsync(l => l.JobId == jobId);
        Assert.Null(lockRecord);

        var acquiredByTwo = await _lockService.TryAcquireAsync(jobId, "instance-2", TimeSpan.FromMinutes(5));
        Assert.True(acquiredByTwo);
    }

    [Fact]
    public async Task RenewAsync_ExtendsLockExpiration()
    {
        var jobId = DefaultId.NewUlid();
        await _lockService.TryAcquireAsync(jobId, "instance-1", TimeSpan.FromMinutes(2));

        var renewed = await _lockService.RenewAsync(jobId, "instance-1", TimeSpan.FromMinutes(15));
        Assert.True(renewed);

        var lockRecord = await _dbContext.Locks.FirstOrDefaultAsync(l => l.JobId == jobId);
        Assert.NotNull(lockRecord);
        Assert.True(lockRecord.ExpiresAt > DateTime.UtcNow.AddMinutes(12));
    }
}
