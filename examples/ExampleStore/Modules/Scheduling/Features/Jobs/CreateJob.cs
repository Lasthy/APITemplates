using Kernel.Common;
using Kernel.Requests;
using Scheduling.Contracts.Dtos;
using Scheduling.Services;

namespace Scheduling.Features.Jobs;

/// <summary>
/// Command to create a new scheduled job.
/// </summary>
public record CreateJob(
    string Name,
    string HandlerType,
    string? CronExpression = null,
    DateTime? ScheduledAt = null,
    string? PayloadJson = null,
    string? Description = null,
    int? TimeoutSeconds = null,
    int? MaxConsecutiveFailures = null,
    int Priority = 100) : IRequest<ScheduledJobDetailDto>;

[Logging(Order = 1)]
public class CreateJobHandler : IRequestHandler<CreateJob, ScheduledJobDetailDto>
{
    private readonly IJobScheduler _scheduler;

    public CreateJobHandler(IJobScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<Result<ScheduledJobDetailDto>> HandleAsync(CreateJob request, CancellationToken cancellationToken = default)
    {
        return _scheduler.CreateJobAsync(
            request.Name,
            request.HandlerType,
            request.CronExpression,
            request.ScheduledAt,
            request.PayloadJson,
            request.Description,
            request.TimeoutSeconds,
            request.MaxConsecutiveFailures,
            request.Priority,
            cancellationToken);
    }
}
