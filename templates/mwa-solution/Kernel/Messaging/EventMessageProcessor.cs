using System.Transactions;
using Kernel.Boxing;
using Kernel.Common;
using Kernel.Events;

namespace Kernel.Messaging;

public class EventMessageProcessor(
	IEventBus _eventBus,
	IEventTypeRegistry _registry
) : IMessageProcessor
{
    public MessageChannel GetChannel()
    {
        return MessageChannel.Event;
    }

    public async Task<Result> ProcessMessage(Message message, CancellationToken cancellationToken = default)
    {
        try {
			using var transactionScope = new TransactionScope();
			
			var type = _registry.Resolve(message.Type);
			var @event = Activator.CreateInstance(type) as IEvent;
			
			if(@event is null) {
				return Result.Failure(new Exception());
			}
			
			await _eventBus.PublishAsync(@event, cancellationToken);
			
			transactionScope.Complete();
			
			return Result.Success();
		}
		catch() {
			
		}
    }
}
