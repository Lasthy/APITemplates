namespace Kernel.Events;

/// <summary>
/// Base type for events to implement.
/// </summary>
public abstract record Event : IEvent
{
	/// <inheritdoc/>
    public DefaultId Id => DefaultId.NewUlid();

	/// <inheritdoc/>
    public DateTime OccurredAt => DateTime.UtcNow;
}
