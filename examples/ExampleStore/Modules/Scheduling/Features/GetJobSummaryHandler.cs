using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts.Dtos;
using Scheduling.Contracts.Requests;
using Scheduling.Persistence;

namespace Scheduling.Features;

/// <summary>
/// Cross-module handler for <see cref="GetJobSummary"/>.
/// Allows other modules to query scheduled job summary information by job ID.
/// </summary>
[Logging(Order = 1)]
public class GetJobSummaryHandler : IRequestHandler<GetJobSummary, ScheduledJobDto>
{
    private readonly SchedulingModuleDbContext _dbContext;

    public GetJobSummaryHandler(SchedulingModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ScheduledJobDto>> HandleAsync(GetJobSummary request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JobId && j.DeletedAt == null, cancellationToken);

        if (job == null)
        {
            return Result.Failure<ScheduledJobDto>(new KeyNotFoundException($"Job '{request.JobId}' not found."));
        }

        return Result.Success(new ScheduledJobDto(
            job.Id,
            job.Name,
            job.HandlerType,
            job.CronExpression,
            job.Status,
            job.NextRunAt,
            job.LastRunAt,
            job.LastRunStatus,
            job.CreatedAt));
    }
}
