using Kernel.Common;

namespace Kernel.Boxing;

/// <summary>
/// Interface for operations related to writing to the Kernel Database messages table.
/// </summary>
public interface IMessageWriter
{
	/// <summary>
	/// Writes a message into the Kernel database to be processed later.
	/// </summary>
	Task<Result> WriteAsync(object content, MessageChannel channel, CancellationToken ct = default);
}
