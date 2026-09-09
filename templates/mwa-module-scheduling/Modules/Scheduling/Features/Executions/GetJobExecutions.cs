using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts.Dtos;
using Scheduling.Persistence;

namespace Scheduling.Features.Executions;

/// <summary>
/// Query to list executions for a specific scheduled job.
/// </summary>
public record GetJobExecutions(DefaultId JobId, int Page = 1, int PageSize = 20) : IRequest<PaginatedResult<JobExecutionDto>>;

[Logging(Order = 1)]
public class GetJobExecutionsHandler : IRequestHandler<GetJobExecutions, PaginatedResult<JobExecutionDto>>
{
    private readonly SchedulingModuleDbContext _dbContext;

    public GetJobExecutionsHandler(SchedulingModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<JobExecutionDto>>> HandleAsync(GetJobExecutions request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.JobId == request.JobId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.StartedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new JobExecutionDto(
                e.Id,
                e.JobId,
                e.Status,
                e.HandlerType,
                e.IsManualTrigger,
                e.StartedAt,
                e.CompletedAt,
                e.DurationMs))
            .ToListAsync(cancellationToken);

        return Result.Success(PaginatedResult<JobExecutionDto>.Create(items, request.Page, request.PageSize, totalCount));
    }
}
