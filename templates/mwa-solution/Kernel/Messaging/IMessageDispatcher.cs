using Kernel.Boxing;
using Kernel.Common;

namespace Kernel.Messaging;

/// <summary>
/// Dispatches persisted outbox messages to their corresponding <see cref="IMessageProcessor"/> by channel.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Dispatches a message to its channel processor and returns the outcome result.
    /// </summary>
    Task<Result> DispatchAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload for dispatching a message.
    /// </summary>
    Task<Result> Dispatch(Message message) => DispatchAsync(message, CancellationToken.None);
}