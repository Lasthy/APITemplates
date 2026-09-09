using Kernel.Common;
using Kernel.Requests;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Command to enable a disabled scheduled job.
/// </summary>
public record EnableJob(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class EnableJobHandler : IRequestHandler<EnableJob, bool>
{
    private readonly IJobScheduler _scheduler;

    public EnableJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<bool>> HandleAsync(EnableJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.EnableJobAsync(request.Id, cancellationToken);
    }
}
