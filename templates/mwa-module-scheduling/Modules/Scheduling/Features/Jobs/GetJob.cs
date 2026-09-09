using Kernel.Common;
using Kernel.Requests;
using Scheduling.Contracts.Dtos;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Query to get detailed information about a single scheduled job.
/// </summary>
public record GetJob(DefaultId Id) : IRequest<ScheduledJobDetailDto>;

[Logging(Order = 1)]
public class GetJobHandler : IRequestHandler<GetJob, ScheduledJobDetailDto>
{
    private readonly IJobScheduler _scheduler;

    public GetJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<ScheduledJobDetailDto>> HandleAsync(GetJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.GetJobAsync(request.Id, cancellationToken);
    }
}
