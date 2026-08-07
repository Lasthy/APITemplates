using Kernel.Boxing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Messaging;

public interface IMessageDispatcher
{
    public Task Dispatch(Message message);
}