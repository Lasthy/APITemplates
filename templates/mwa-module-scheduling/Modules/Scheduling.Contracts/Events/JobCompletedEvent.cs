using Kernel.Events;

namespace Scheduling.Contracts.Events;

/// <summary>
/// Event published when a scheduled job completes successfully.
/// </summary>
public record JobCompletedEvent(
    DefaultId JobId,
    DefaultId ExecutionId,
    string HandlerType,
    long DurationMs) : Event;
