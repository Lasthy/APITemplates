using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts.Dtos;
using Scheduling.Persistence;

namespace Scheduling.Features.Executions;

/// <summary>
/// Query to get full execution details by execution ID.
/// </summary>
public record GetExecution(DefaultId Id) : IRequest<JobExecutionDetailDto>;

[Logging(Order = 1)]
public class GetExecutionHandler : IRequestHandler<GetExecution, JobExecutionDetailDto>
{
    private readonly SchedulingModuleDbContext _dbContext;

    public GetExecutionHandler(SchedulingModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<JobExecutionDetailDto>> HandleAsync(GetExecution request, CancellationToken cancellationToken = default)
    {
        var execution = await _dbContext.Executions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (execution == null)
        {
            return Result.Failure<JobExecutionDetailDto>(new KeyNotFoundException($"Execution '{request.Id}' not found."));
        }

        return Result.Success(new JobExecutionDetailDto(
            execution.Id,
            execution.JobId,
            execution.Status,
            execution.HandlerType,
            execution.PayloadJson,
            execution.ResultJson,
            execution.Error,
            execution.StackTrace,
            execution.IsManualTrigger,
            execution.ExecutedByInstance,
            execution.StartedAt,
            execution.CompletedAt,
            execution.DurationMs));
    }
}
