namespace Scheduling.Contracts.Dtos;

/// <summary>
/// Full detail DTO for a job execution including payload, result, errors, and environment diagnostics.
/// </summary>
public record JobExecutionDetailDto(
    DefaultId Id,
    DefaultId JobId,
    string Status,
    string HandlerType,
    string? PayloadJson,
    string? ResultJson,
    string? Error,
    string? StackTrace,
    bool IsManualTrigger,
    string? ExecutedByInstance,
    DateTime StartedAt,
    DateTime? CompletedAt,
    long? DurationMs);
