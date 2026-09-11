using Kernel.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kernel.Tests.Events;

public class InProcessEventBusTests
{
    private record OrderCreatedEvent(string OrderNumber) : Event;
    private record SpecialOrderCreatedEvent(string OrderNumber, bool IsVip) : OrderCreatedEvent(OrderNumber);

    private class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
    {
        public List<OrderCreatedEvent> Handled { get; } = new();

        public Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }

    private class SpecialOrderCreatedHandler : IEventHandler<SpecialOrderCreatedEvent>
    {
        public List<SpecialOrderCreatedEvent> Handled { get; } = new();

        public Task HandleAsync(SpecialOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeRegisteredHandler()
    {
        var services = new ServiceCollection();
        var handler = new OrderCreatedHandler();
        services.AddSingleton<IEventHandler<OrderCreatedEvent>>(handler);

        var sp = services.BuildServiceProvider();
        var bus = new InProcessEventBus(sp);

        var @event = new OrderCreatedEvent("ORD-1001");
        await bus.PublishAsync(@event);

        Assert.Single(handler.Handled);
        Assert.Equal("ORD-1001", handler.Handled[0].OrderNumber);
    }

    [Fact]
    public async Task PublishAsync_PolymorphicDispatch_ShouldInvokeBaseHandler()
    {
        var services = new ServiceCollection();
        var baseHandler = new OrderCreatedHandler();
        var specialHandler = new SpecialOrderCreatedHandler();

        services.AddSingleton<IEventHandler<OrderCreatedEvent>>(baseHandler);
        services.AddSingleton<IEventHandler<SpecialOrderCreatedEvent>>(specialHandler);

        var sp = services.BuildServiceProvider();
        var bus = new InProcessEventBus(sp);

        var @event = new SpecialOrderCreatedEvent("ORD-VIP-99", IsVip: true);
        await bus.PublishAsync(@event);

        // Both specific and base handler should have been executed
        Assert.Single(specialHandler.Handled);
        Assert.Single(baseHandler.Handled);
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_ShouldInvokeAllHandlers()
    {
        var services = new ServiceCollection();
        var handler1 = new OrderCreatedHandler();
        var handler2 = new OrderCreatedHandler();

        services.AddSingleton<IEventHandler<OrderCreatedEvent>>(handler1);
        services.AddSingleton<IEventHandler<OrderCreatedEvent>>(handler2);

        var sp = services.BuildServiceProvider();
        var bus = new InProcessEventBus(sp);

        await bus.PublishAsync(new OrderCreatedEvent("ORD-123"));

        Assert.Single(handler1.Handled);
        Assert.Single(handler2.Handled);
    }
}
