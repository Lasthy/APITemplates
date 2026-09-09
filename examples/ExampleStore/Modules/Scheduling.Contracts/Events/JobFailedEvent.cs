using Kernel.Events;

namespace Scheduling.Contracts.Events;

/// <summary>
/// Event published when a scheduled job execution fails.
/// </summary>
public record JobFailedEvent(
    DefaultId JobId,
    DefaultId ExecutionId,
    string HandlerType,
    string Error) : Event;
