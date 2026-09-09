using Kernel.Requests;
using Scheduling.Contracts.Dtos;

namespace Scheduling.Contracts.Requests;

/// <summary>
/// Programmatically create/schedule a job from another module.
/// </summary>
public record ScheduleJob(
    string Name,
    string HandlerType,
    string? CronExpression = null,
    DateTime? ScheduledAt = null,
    string? PayloadJson = null,
    string? Description = null,
    int? TimeoutSeconds = null,
    int Priority = 100) : IRequest<ScheduledJobDto>;
