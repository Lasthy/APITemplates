using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scheduling.Entities;
using Scheduling.Persistence;

namespace Scheduling.Services;

/// <summary>
/// Database-backed implementation of <see cref="IJobLockService"/>.
/// </summary>
public class JobLockService : IJobLockService
{
    private readonly SchedulingModuleDbContext _dbContext;
    private readonly ILogger<JobLockService> _logger;

    public JobLockService(SchedulingModuleDbContext dbContext, ILogger<JobLockService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(DefaultId jobId, string instanceId, TimeSpan lockDuration, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lockDuration);

        try
        {
            var existingLock = await _dbContext.Locks
                .FirstOrDefaultAsync(l => l.JobId == jobId, ct);

            if (existingLock == null)
            {
                var newLock = new JobLock
                {
                    JobId = jobId,
                    LockedByInstance = instanceId,
                    AcquiredAt = now,
                    ExpiresAt = expiresAt
                };
                _dbContext.Locks.Add(newLock);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogDebug("Instance {InstanceId} acquired new lock for Job {JobId} until {ExpiresAt}.", instanceId, jobId, expiresAt);
                return true;
            }

            if (existingLock.ExpiresAt <= now)
            {
                // Lock expired - steal it
                _logger.LogWarning("Lock for Job {JobId} expired at {ExpiredAt}. Stealing lock for instance {InstanceId}.", jobId, existingLock.ExpiresAt, instanceId);
                existingLock.LockedByInstance = instanceId;
                existingLock.AcquiredAt = now;
                existingLock.ExpiresAt = expiresAt;
                await _dbContext.SaveChangesAsync(ct);
                return true;
            }

            if (existingLock.LockedByInstance == instanceId)
            {
                // Already held by this instance - extend
                existingLock.ExpiresAt = expiresAt;
                await _dbContext.SaveChangesAsync(ct);
                return true;
            }

            // Held by another active instance
            return false;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex, "Failed to acquire lock for Job {JobId} due to concurrency collision.", jobId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock for Job {JobId}.", jobId);
            return false;
        }
    }

    public async Task ReleaseAsync(DefaultId jobId, string instanceId, CancellationToken ct = default)
    {
        try
        {
            var existingLock = await _dbContext.Locks
                .FirstOrDefaultAsync(l => l.JobId == jobId, ct);

            if (existingLock != null && existingLock.LockedByInstance == instanceId)
            {
                _dbContext.Locks.Remove(existingLock);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogDebug("Instance {InstanceId} released lock for Job {JobId}.", instanceId, jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock for Job {JobId} held by {InstanceId}.", jobId, instanceId);
        }
    }

    public async Task<bool> RenewAsync(DefaultId jobId, string instanceId, TimeSpan extension, CancellationToken ct = default)
    {
        try
        {
            var existingLock = await _dbContext.Locks
                .FirstOrDefaultAsync(l => l.JobId == jobId, ct);

            if (existingLock != null && existingLock.LockedByInstance == instanceId)
            {
                existingLock.ExpiresAt = DateTime.UtcNow.Add(extension);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogDebug("Instance {InstanceId} renewed lock for Job {JobId} until {ExpiresAt}.", instanceId, jobId, existingLock.ExpiresAt);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing lock for Job {JobId} held by {InstanceId}.", jobId, instanceId);
            return false;
        }
    }
}
