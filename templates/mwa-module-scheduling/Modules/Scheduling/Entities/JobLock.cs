namespace Scheduling.Entities;

/// <summary>
/// Distributed lock record for multi-instance scheduler safety.
/// </summary>
public class JobLock
{
    public DefaultId JobId { get; set; }

    /// <summary>
    /// The instance identifier that holds the lock.
    /// </summary>
    public string LockedByInstance { get; set; } = string.Empty;

    /// <summary>
    /// When the lock was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// When the lock expires (heartbeat-based; if not renewed, other instances can steal it).
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
