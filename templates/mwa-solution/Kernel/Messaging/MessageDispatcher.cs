using Kernel.Boxing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Messaging;

public class MessageDispatcher : IMessageDispatcher
{
    private readonly IMessageProcessor[] _messageProcessors;
    
    public MessageDispatcher(IServiceProvider serviceProvider)
    {
        _messageProcessors = serviceProvider.GetServices<IMessageProcessor>().ToArray();
    }
    
    public async Task Dispatch(Message message)
    {
        foreach (var processor in _messageProcessors)
        {
            if(processor.GetChannel() == message.Channel)
            {
                await processor.ProcessMessage(message);

                break;
            }
        }
    }
}
