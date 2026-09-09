using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts.Dtos;
using Scheduling.Persistence;

namespace Scheduling.Features.Executions;

/// <summary>
/// Query to retrieve paginated job executions with optional filtering by job, status, and dates.
/// </summary>
public record GetExecutions(
    DefaultId? JobId = null,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<JobExecutionDto>>;

[Logging(Order = 1)]
public class GetExecutionsHandler : IRequestHandler<GetExecutions, PaginatedResult<JobExecutionDto>>
{
    private readonly SchedulingModuleDbContext _dbContext;

    public GetExecutionsHandler(SchedulingModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<JobExecutionDto>>> HandleAsync(GetExecutions request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Executions.AsNoTracking();

        if (request.JobId.HasValue && request.JobId.Value != DefaultId.Empty)
        {
            query = query.Where(e => e.JobId == request.JobId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(e => e.Status == request.Status);
        }

        if (request.From.HasValue)
        {
            query = query.Where(e => e.StartedAt >= request.From.Value.ToUniversalTime());
        }

        if (request.To.HasValue)
        {
            query = query.Where(e => e.StartedAt <= request.To.Value.ToUniversalTime());
        }

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
