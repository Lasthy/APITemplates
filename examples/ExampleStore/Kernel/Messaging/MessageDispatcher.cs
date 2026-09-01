using Kernel.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging;

/// <summary>
/// Default implementation of <see cref="IMessageDispatcher"/> that routes messages to their registered channel processor.
/// </summary>
public class MessageDispatcher : IMessageDispatcher
{
    private readonly IMessageProcessor[] _messageProcessors;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(IServiceProvider serviceProvider, ILogger<MessageDispatcher> logger)
    {
        _messageProcessors = serviceProvider.GetServices<IMessageProcessor>().ToArray();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> DispatchAsync(Message message, CancellationToken cancellationToken = default)
    {
        var processor = _messageProcessors.FirstOrDefault(p => p.GetChannel() == message.Channel);

        if (processor is null)
        {
            var ex = new InvalidOperationException($"No processor registered for message channel '{message.Channel}'.");
            _logger.LogError(ex, "Failed to dispatch message {MessageId}: unsupported channel {Channel}.", message.Id, message.Channel);
            return Result.Failure(ex);
        }

        try
        {
            return await processor.ProcessMessage(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while dispatching message {MessageId} to channel {Channel}.", message.Id, message.Channel);
            return Result.Failure(ex);
        }
    }
}
