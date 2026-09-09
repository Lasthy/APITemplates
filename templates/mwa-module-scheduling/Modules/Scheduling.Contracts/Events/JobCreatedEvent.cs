using Kernel.Events;

namespace Scheduling.Contracts.Events;

/// <summary>
/// Event published when a new scheduled job is registered.
/// </summary>
public record JobCreatedEvent(
    DefaultId JobId,
    string Name,
    string HandlerType,
    string? CronExpression) : Event;
