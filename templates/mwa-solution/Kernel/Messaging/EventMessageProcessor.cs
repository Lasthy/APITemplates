using System.Transactions;
using Kernel.Common;
using Kernel.Events;
using Kernel.Json;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging;

/// <summary>
/// Processes <see cref="MessageChannel.Event"/> messages from the outbox.
/// Uses <see cref="IInbox"/> to guarantee idempotency, deserializes the message content,
/// resolves the event type, and publishes to <see cref="IEventBus"/>.
/// </summary>
public class EventMessageProcessor(
    IEventBus _eventBus,
    IEventTypeRegistry _registry,
    IJsonSerializer _jsonSerializer,
    IInbox _inbox,
    ILogger<EventMessageProcessor> _logger
) : IMessageProcessor
{
    public MessageChannel GetChannel() => MessageChannel.Event;

    public async Task<Result> ProcessMessage(Message message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing message: Id={MessageId}, Channel={Channel}, Type={Type}",
                message.Id, message.Channel, message.Type);

            if (await _inbox.HasBeenProcessedAsync(message.Id, cancellationToken))
            {
                _logger.LogInformation(
                    "Message Id={MessageId} has already been processed in the inbox. Skipping duplicate execution.",
                    message.Id);
                return Result.Success();
            }

            using var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var type = _registry.Resolve(message.Type);
            var @event = _jsonSerializer.Deserialize(message.Content, type) as IEvent;

            if (@event is null)
            {
                return Result.Failure(
                    new InvalidOperationException(
                        $"Message {message.Id} content could not be deserialized to IEvent (type: {message.Type})."));
            }

            await _eventBus.PublishAsync(@event, cancellationToken);

            await _inbox.MarkProcessedAsync(message.Id, cancellationToken);

            transactionScope.Complete();

            _logger.LogInformation("Processed message Id={MessageId} successfully.", message.Id);

            return Result.Success();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing message Id={MessageId}.", message.Id);

            return Result.Failure(e);
        }
    }
}

