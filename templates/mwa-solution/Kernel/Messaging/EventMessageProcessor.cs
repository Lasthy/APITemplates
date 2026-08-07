using System.Transactions;
using Kernel.Boxing;
using Kernel.Common;
using Kernel.Events;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging;

public class EventMessageProcessor(
	IEventBus _eventBus,
	IEventTypeRegistry _registry,
	ILogger<EventMessageProcessor> _logger
) : IMessageProcessor
{
    public MessageChannel GetChannel()
    {
        return MessageChannel.Event;
    }

    public async Task<Result> ProcessMessage(Message message, CancellationToken cancellationToken = default)
    {
        try {
			_logger.LogInformation("Processing message: Id: {}, Channel {}, Type: {}", message.Id, message.Channel, message.GetType());

			using var transactionScope = new TransactionScope();
			
			var type = _registry.Resolve(message.Type);
			var @event = Activator.CreateInstance(type) as IEvent;
			
			if(@event is null) {
				return Result.Failure(new Exception());
			}
			
			await _eventBus.PublishAsync(@event, cancellationToken);
			
			transactionScope.Complete();

            _logger.LogInformation("Processed message of Id {} successfully.", message.Id);

            return Result.Success();
		}
		catch(Exception e) {
            _logger.LogInformation("Error while processing message of Id {}.", message.Id);
			
			return Result.Failure(e);
        }
    }
}
