using Kernel.Configuration;
using Kernel.Json;

namespace Scheduling.Configuration;

/// <summary>
/// Dynamic settings section for the Scheduling module.
/// Loaded from <c>appsettings.Scheduling.json</c> with optional <c>appsettings.Scheduling.{Environment}.json</c> overlay.
/// </summary>
public class SchedulingSettings : DynamicSettingsSection
{
    public override string SectionName => "Scheduling";

    /// <summary>
    /// How often the worker checks for due jobs (in seconds).
    /// Defaults to 15.
    /// </summary>
    public int PollingIntervalSeconds => TryGetValue<int>(nameof(PollingIntervalSeconds), out var val) && val > 0
        ? val
        : 15;

    /// <summary>
    /// Maximum jobs executing simultaneously on this instance.
    /// Defaults to 5.
    /// </summary>
    public int MaxConcurrentJobs => TryGetValue<int>(nameof(MaxConcurrentJobs), out var val) && val > 0
        ? val
        : 5;

    /// <summary>
    /// Default handler timeout in seconds.
    /// Defaults to 300 (5 minutes).
    /// </summary>
    public int DefaultTimeoutSeconds => TryGetValue<int>(nameof(DefaultTimeoutSeconds), out var val) && val > 0
        ? val
        : 300;

    /// <summary>
    /// Default consecutive failures before auto-disabling a job.
    /// Defaults to 5.
    /// </summary>
    public int DefaultMaxConsecutiveFailures => TryGetValue<int>(nameof(DefaultMaxConsecutiveFailures), out var val) && val > 0
        ? val
        : 5;

    /// <summary>
    /// Lock duration in seconds before lock expiry.
    /// Defaults to 600 (10 minutes).
    /// </summary>
    public int LockDurationSeconds => TryGetValue<int>(nameof(LockDurationSeconds), out var val) && val > 0
        ? val
        : 600;

    /// <summary>
    /// Interval in seconds for running jobs to renew their distributed lock.
    /// Defaults to 60 (1 minute).
    /// </summary>
    public int LockHeartbeatIntervalSeconds => TryGetValue<int>(nameof(LockHeartbeatIntervalSeconds), out var val) && val > 0
        ? val
        : 60;

    /// <summary>
    /// Days to retain historical execution records before purging.
    /// Defaults to 30.
    /// </summary>
    public int ExecutionRetentionDays => TryGetValue<int>(nameof(ExecutionRetentionDays), out var val) && val > 0
        ? val
        : 30;

    /// <summary>
    /// Interval in hours between cleanup worker runs.
    /// Defaults to 24.
    /// </summary>
    public int CleanupIntervalHours => TryGetValue<int>(nameof(CleanupIntervalHours), out var val) && val > 0
        ? val
        : 24;

    /// <summary>
    /// Policy for missed job executions after downtime: "FireOnce", "SkipToNext", "FireAll".
    /// Defaults to "FireOnce".
    /// </summary>
    public string MissedFirePolicy => TryGetValue<string>(nameof(MissedFirePolicy), out var val) && !string.IsNullOrWhiteSpace(val)
        ? val
        : "FireOnce";

    /// <summary>
    /// Whether to log and persist full exception stack traces on failed executions.
    /// Defaults to false.
    /// </summary>
    public bool EnableStackTraceLogging => TryGetValue<bool>(nameof(EnableStackTraceLogging), out var val) && val;

    public SchedulingSettings(IJsonSerializer jsonSerializer, string? environment = null)
        : base("appsettings.Scheduling.json", jsonSerializer, environment)
    {
    }
}
