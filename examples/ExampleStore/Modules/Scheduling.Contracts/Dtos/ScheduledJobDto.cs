namespace Scheduling.Contracts.Dtos;

/// <summary>
/// Summary DTO for a scheduled job.
/// </summary>
public record ScheduledJobDto(
    DefaultId Id,
    string Name,
    string HandlerType,
    string? CronExpression,
    string Status,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    DateTime CreatedAt);
