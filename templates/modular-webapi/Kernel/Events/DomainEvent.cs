namespace Kernel.Events;

/// <summary>
/// Base type for domain events to implement.
/// </summary>
public abstract record DomainEvent : IEvent
{
	/// <inheritdoc/>
    public DefaultId Id => DefaultId.NewUlid();

	/// <inheritdoc/>
    public DateTime OccurredAt => DateTime.UtcNow;
}
