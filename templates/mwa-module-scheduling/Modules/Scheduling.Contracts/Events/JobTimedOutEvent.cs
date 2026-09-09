using Kernel.Events;

namespace Scheduling.Contracts.Events;

/// <summary>
/// Event published when a scheduled job execution exceeds its configured timeout.
/// </summary>
public record JobTimedOutEvent(
    DefaultId JobId,
    DefaultId ExecutionId,
    string HandlerType,
    int TimeoutSeconds) : Event;
