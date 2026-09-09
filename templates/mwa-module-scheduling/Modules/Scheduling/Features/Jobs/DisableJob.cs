using Kernel.Common;
using Kernel.Requests;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Command to disable an active scheduled job.
/// </summary>
public record DisableJob(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class DisableJobHandler : IRequestHandler<DisableJob, bool>
{
    private readonly IJobScheduler _scheduler;

    public DisableJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<bool>> HandleAsync(DisableJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.DisableJobAsync(request.Id, cancellationToken);
    }
}
