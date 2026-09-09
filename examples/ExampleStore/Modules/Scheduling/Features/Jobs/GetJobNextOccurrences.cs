using Kernel.Common;
using Kernel.Requests;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Query to preview the next N occurrences of a job's schedule.
/// </summary>
public record GetJobNextOccurrences(DefaultId Id, int Count = 5) : IRequest<IReadOnlyList<DateTime>>;

[Logging(Order = 1)]
public class GetJobNextOccurrencesHandler : IRequestHandler<GetJobNextOccurrences, IReadOnlyList<DateTime>>
{
    private readonly IJobScheduler _scheduler;

    public GetJobNextOccurrencesHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<IReadOnlyList<DateTime>>> HandleAsync(GetJobNextOccurrences request, CancellationToken cancellationToken = default)
    {
        return _scheduler.GetNextOccurrencesAsync(request.Id, request.Count, cancellationToken);
    }
}
