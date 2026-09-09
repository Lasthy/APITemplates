using Kernel.Common;
using Kernel.Requests;
using Scheduling.Contracts.Dtos;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Command to update an existing scheduled job.
/// </summary>
public record UpdateJob(
    DefaultId Id,
    string? Name = null,
    string? Description = null,
    string? CronExpression = null,
    string? PayloadJson = null,
    int? TimeoutSeconds = null,
    int? Priority = null,
    int? MaxConsecutiveFailures = null) : IRequest<ScheduledJobDetailDto>;

[Logging(Order = 1)]
public class UpdateJobHandler : IRequestHandler<UpdateJob, ScheduledJobDetailDto>
{
    private readonly IJobScheduler _scheduler;

    public UpdateJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<ScheduledJobDetailDto>> HandleAsync(UpdateJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.UpdateJobAsync(
            request.Id,
            request.Name,
            request.Description,
            request.CronExpression,
            request.PayloadJson,
            request.TimeoutSeconds,
            request.Priority,
            request.MaxConsecutiveFailures,
            cancellationToken);
    }
}
