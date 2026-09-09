namespace Scheduling.Handlers;

/// <summary>
/// Interface for scheduled job handler implementations.
/// Modules register concrete implementations that are resolved at execution time.
/// </summary>
public interface IScheduledJobHandler
{
    /// <summary>
    /// Executes the job logic.
    /// </summary>
    /// <param name="context">Execution context with payload, job ID, and service provider.</param>
    /// <param name="ct">Cancellation token triggered on worker shutdown or timeout.</param>
    Task<JobHandlerResult> HandleAsync(JobExecutionContext context, CancellationToken ct);
}

/// <summary>
/// Context provided to job handlers during execution.
/// </summary>
public record JobExecutionContext(
    DefaultId JobId,
    DefaultId ExecutionId,
    string? PayloadJson,
    IServiceProvider Services);

/// <summary>
/// Result returned by job handlers.
/// </summary>
public record JobHandlerResult(
    bool IsSuccess,
    string? ResultJson = null,
    string? Error = null)
{
    public static JobHandlerResult Success(string? resultJson = null) => new(true, resultJson);
    public static JobHandlerResult Failure(string error) => new(false, Error: error);
}
