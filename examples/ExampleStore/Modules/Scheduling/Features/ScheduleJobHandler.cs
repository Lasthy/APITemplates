using Kernel.Common;
using Kernel.Requests;
using Scheduling.Contracts.Dtos;
using Scheduling.Contracts.Requests;
using Scheduling.Services;

namespace Scheduling.Features;

/// <summary>
/// Cross-module handler for <see cref="ScheduleJob"/>.
/// Allows other modules to programmatically create and schedule jobs.
/// </summary>
[Logging(Order = 1)]
public class ScheduleJobHandler : IRequestHandler<ScheduleJob, ScheduledJobDto>
{
    private readonly IJobScheduler _scheduler;

    public ScheduleJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public async Task<Result<ScheduledJobDto>> HandleAsync(ScheduleJob request, CancellationToken cancellationToken = default)
    {
        var result = await _scheduler.CreateJobAsync(
            request.Name,
            request.HandlerType,
            request.CronExpression,
            request.ScheduledAt,
            request.PayloadJson,
            request.Description,
            request.TimeoutSeconds,
            maxConsecutiveFailures: null,
            request.Priority,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<ScheduledJobDto>(result.Error!);
        }

        var detail = result.Value!;
        return Result.Success(new ScheduledJobDto(
            detail.Id,
            detail.Name,
            detail.HandlerType,
            detail.CronExpression,
            detail.Status,
            detail.NextRunAt,
            detail.LastRunAt,
            detail.LastRunStatus,
            detail.CreatedAt));
    }
}
