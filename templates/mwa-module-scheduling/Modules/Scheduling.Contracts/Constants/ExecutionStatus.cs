namespace Scheduling.Contracts.Constants;

/// <summary>
/// Status values for job executions.
/// </summary>
public static class ExecutionStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string TimedOut = "TimedOut";
    public const string Cancelled = "Cancelled";
}
