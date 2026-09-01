namespace Kernel.Messaging;

/// <summary>
/// Channels that messages can belong to.
/// </summary>
public enum MessageChannel
{
	/// <summary>
    /// Will be treated as an event and will be sent to the event bus to be handled.
    /// </summary>
	Event,
	
	/// <summary>
    /// Will be treated as an e-mail and will be processed by a dedicated service.
    /// </summary>
	Email
}
