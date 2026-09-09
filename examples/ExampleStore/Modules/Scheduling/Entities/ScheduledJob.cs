using Kernel.Entities;
using Scheduling.Contracts.Constants;

namespace Scheduling.Entities;

/// <summary>
/// Definition of a scheduled job (recurring cron or one-shot delayed).
/// </summary>
public class ScheduledJob : IAuditable, ISoftDeletable
{
    public DefaultId Id { get; set; }

    /// <summary>
    /// Human-readable name for this job (e.g., "Daily Report Generation").
    /// Unique within the application.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// CLR type name of the IScheduledJobHandler implementation to invoke.
    /// Resolved via JobHandlerRegistry at execution time.
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// Standard 5-field cron expression (minute hour day-of-month month day-of-week).
    /// Null for one-shot jobs that execute at ScheduledAt and don't recur.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// For one-shot jobs: the exact UTC datetime to execute.
    /// For cron jobs: null (computed from cron expression).
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Serialized JSON payload passed to the handler on execution.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// Current status: Enabled, Disabled, Running.
    /// </summary>
    public string Status { get; set; } = JobStatus.Enabled;

    /// <summary>
    /// Computed next UTC datetime this job should run.
    /// Updated after each execution (cron) or set on creation (one-shot).
    /// </summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>
    /// Last time this job was executed (regardless of outcome).
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Status of the last execution: Succeeded, Failed, TimedOut.
    /// </summary>
    public string? LastRunStatus { get; set; }

    /// <summary>
    /// Total number of executions (lifetime).
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Total number of failed executions (lifetime).
    /// </summary>
    public long TotalFailures { get; set; }

    /// <summary>
    /// Maximum execution duration in seconds before the job is considered timed out.
    /// Null uses the global default from SchedulingSettings.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Maximum number of consecutive failures before the job is auto-disabled.
    /// Null uses the global default from SchedulingSettings.
    /// </summary>
    public int? MaxConsecutiveFailures { get; set; }

    /// <summary>
    /// Current consecutive failure count. Resets on success.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Whether this is a one-shot job (executes once, then is auto-disabled).
    /// </summary>
    public bool IsOneShot { get; set; }

    /// <summary>
    /// Priority level (lower = higher priority). Used for ordering when
    /// multiple jobs are due simultaneously and MaxConcurrentJobs is limited.
    /// </summary>
    public int Priority { get; set; } = 100;

    // ── IAuditable ──
    public DateTime CreatedAt { get; set; }
    public DefaultId? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DefaultId? UpdatedBy { get; set; }

    // ── ISoftDeletable ──
    public DateTime? DeletedAt { get; set; }
    public DefaultId? DeletedBy { get; set; }

    // ── Navigation ──
    public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();
}
