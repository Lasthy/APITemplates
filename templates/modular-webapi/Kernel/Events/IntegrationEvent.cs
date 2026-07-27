namespace Kernel.Events;

/// <summary>
/// Base type for integration events to implement.
/// </summary>
public abstract record IntegrationEvent : IEvent
{
	/// <inheritdoc/>
    public DefaultId Id => DefaultId.NewUlid();

	/// <inheritdoc/>
    public DateTime OccurredAt => DateTime.UtcNow;
}