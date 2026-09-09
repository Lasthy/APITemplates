using Kernel.Common;
using Kernel.Requests;
using Scheduling.Contracts.Dtos;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Command to manually trigger an immediate execution of a scheduled job.
/// </summary>
public record TriggerJob(DefaultId Id) : IRequest<JobExecutionDetailDto>;

[Logging(Order = 1)]
public class TriggerJobHandler : IRequestHandler<TriggerJob, JobExecutionDetailDto>
{
    private readonly IJobScheduler _scheduler;

    public TriggerJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<JobExecutionDetailDto>> HandleAsync(TriggerJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.TriggerJobAsync(request.Id, cancellationToken);
    }
}
