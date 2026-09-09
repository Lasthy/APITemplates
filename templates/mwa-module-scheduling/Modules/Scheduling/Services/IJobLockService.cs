namespace Scheduling.Services;

/// <summary>
/// Service for acquiring, renewing, and releasing distributed pessimistic job locks.
/// Ensures that in multi-instance deployments, only a single instance executes a given job at any time.
/// </summary>
public interface IJobLockService
{
    /// <summary>
    /// Attempts to acquire an exclusive lock for a job.
    /// Returns true if the lock was acquired or renewed; false if another instance holds an active lock.
    /// </summary>
    Task<bool> TryAcquireAsync(DefaultId jobId, string instanceId, TimeSpan lockDuration, CancellationToken ct = default);

    /// <summary>
    /// Releases the lock for a job if held by the given instance.
    /// </summary>
    Task ReleaseAsync(DefaultId jobId, string instanceId, CancellationToken ct = default);

    /// <summary>
    /// Renews the lock (heartbeat) to prevent expiry during long-running jobs.
    /// </summary>
    Task<bool> RenewAsync(DefaultId jobId, string instanceId, TimeSpan extension, CancellationToken ct = default);
}
