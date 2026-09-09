using Kernel.Events;

namespace Scheduling.Contracts.Events;

/// <summary>
/// Event published when a job is triggered manually out-of-band from its schedule.
/// </summary>
public record JobTriggeredManuallyEvent(
    DefaultId JobId,
    DefaultId TriggeredBy) : Event;
