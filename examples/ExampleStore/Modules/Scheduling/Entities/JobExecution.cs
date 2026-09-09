using Scheduling.Contracts.Constants;

namespace Scheduling.Entities;

/// <summary>
/// Record of a job execution attempt.
/// </summary>
public class JobExecution
{
    public DefaultId Id { get; set; }
    public DefaultId JobId { get; set; }

    /// <summary>
    /// Status: Pending, Running, Succeeded, Failed, TimedOut, Cancelled.
    /// </summary>
    public string Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>
    /// The handler type that was invoked.
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// The payload JSON that was passed to the handler.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// Optional output/result from the handler (limited to 8KB).
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Stack trace if the execution failed (Development only; redacted in Production).
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Whether this execution was triggered manually (vs. scheduled).
    /// </summary>
    public bool IsManualTrigger { get; set; }

    /// <summary>
    /// The instance/partition that executed this job (for multi-instance debugging).
    /// </summary>
    public string? ExecutedByInstance { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    // ── Navigation ──
    public ScheduledJob Job { get; set; } = null!;
}
