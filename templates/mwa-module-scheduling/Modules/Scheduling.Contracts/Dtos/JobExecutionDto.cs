namespace Scheduling.Contracts.Dtos;

/// <summary>
/// Summary DTO for a job execution.
/// </summary>
public record JobExecutionDto(
    DefaultId Id,
    DefaultId JobId,
    string Status,
    string HandlerType,
    bool IsManualTrigger,
    DateTime StartedAt,
    DateTime? CompletedAt,
    long? DurationMs);
