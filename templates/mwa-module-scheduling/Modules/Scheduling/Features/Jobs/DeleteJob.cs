using Kernel.Common;
using Kernel.Requests;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Command to soft-delete a scheduled job.
/// </summary>
public record DeleteJob(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class DeleteJobHandler : IRequestHandler<DeleteJob, bool>
{
    private readonly IJobScheduler _scheduler;

    public DeleteJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<bool>> HandleAsync(DeleteJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.DeleteJobAsync(request.Id, cancellationToken);
    }
}
