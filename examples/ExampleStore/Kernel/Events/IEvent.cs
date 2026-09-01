namespace Kernel.Events;

/// <summary>
/// Marker interface for events.
/// </summary>
public interface IEvent
{
	/// <summary>
    /// The event Id.
    /// </summary>
    DefaultId Id { get; }
	
	/// <summary>
    /// When the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }
}
