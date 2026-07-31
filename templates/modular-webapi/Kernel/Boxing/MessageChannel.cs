namespace Kernel.Boxing;

/// <summary>
/// Channels that messages can belong to
/// </summary>
public enum MessageChannel
{
	/// <summary>
    /// Will be treated as an event and will be sent to the event bus to be handled
    /// </summary>
	Event,
	
	/// <summary>
    /// Will be treated as an e-mail and will be queried by and dedicated hosted service.
    /// </summary>
	Email
}
