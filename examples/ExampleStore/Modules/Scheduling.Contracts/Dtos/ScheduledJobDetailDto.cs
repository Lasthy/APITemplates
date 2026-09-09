namespace Scheduling.Contracts.Dtos;

/// <summary>
/// Full detail DTO for a scheduled job including configuration, statistics, and execution timing.
/// </summary>
public record ScheduledJobDetailDto(
    DefaultId Id,
    string Name,
    string? Description,
    string HandlerType,
    string? CronExpression,
    DateTime? ScheduledAt,
    string? PayloadJson,
    string Status,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    long TotalExecutions,
    long TotalFailures,
    int ConsecutiveFailures,
    int? TimeoutSeconds,
    int Priority,
    bool IsOneShot,
    DateTime CreatedAt);
