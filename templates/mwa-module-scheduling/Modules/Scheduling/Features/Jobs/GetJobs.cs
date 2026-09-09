using Kernel.Common;
using Kernel.Requests;
using Scheduling.Contracts.Dtos;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Query to list scheduled jobs with optional status/handler filters and pagination.
/// </summary>
public record GetJobs(
    string? Status = null,
    string? HandlerType = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<ScheduledJobDto>>;

[Logging(Order = 1)]
public class GetJobsHandler : IRequestHandler<GetJobs, PaginatedResult<ScheduledJobDto>>
{
    private readonly IJobScheduler _scheduler;

    public GetJobsHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public async Task<Result<PaginatedResult<ScheduledJobDto>>> HandleAsync(GetJobs request, CancellationToken cancellationToken = default)
    {
        var result = await _scheduler.GetJobsAsync(
            request.Status,
            request.HandlerType,
            request.Page,
            request.PageSize,
            cancellationToken);

        return Result.Success(result);
    }
}
