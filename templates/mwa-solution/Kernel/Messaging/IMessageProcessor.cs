using Kernel.Common;

namespace Kernel.Boxing;

/// <summary>
/// Processes a message of a certain channel.
/// </summary>
public interface IMessageProcessor
{
	/// <summary>
	/// Gets the channel of the processor.
	/// </summary>
	MessageChannel GetChannel();
	
	/// <summary>
    /// Processes a message.
    /// </summary>
	Task<Result> ProcessMessage(Message message, CancellationToken cancellationToken = default);
}
