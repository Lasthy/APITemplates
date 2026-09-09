namespace Scheduling.Handlers.BuiltIn;

/// <summary>
/// Default no-op handler for testing or placeholder scheduled jobs.
/// </summary>
public class NoOpJobHandler : IScheduledJobHandler
{
    public Task<JobHandlerResult> HandleAsync(JobExecutionContext context, CancellationToken ct)
    {
        return Task.FromResult(JobHandlerResult.Success("No-op job executed successfully."));
    }
}
