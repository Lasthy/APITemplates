using Kernel.Events;
using Xunit;

namespace Kernel.Tests.Events;

public class EventTypeRegistryTests
{
    private record SampleEvent : Event;

    [Fact]
    public void Register_And_Resolve_ShouldReturnCorrectType()
    {
        var registry = new EventTypeRegistry();
        registry.Register<SampleEvent>();

        var fullName = typeof(SampleEvent).FullName!;
        var resolved = registry.Resolve(fullName);

        Assert.Equal(typeof(SampleEvent), resolved);
    }

    [Fact]
    public void Resolve_UnregisteredType_ShouldThrowInvalidOperationException()
    {
        var registry = new EventTypeRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Resolve("NonExistent.EventType"));
    }

    [Fact]
    public void RegisterFromAssembly_ShouldDiscoverEvents()
    {
        var registry = new EventTypeRegistry();
        registry.RegisterFromAssembly(typeof(EventTypeRegistryTests).Assembly);

        var fullName = typeof(SampleEvent).FullName!;
        var resolved = registry.Resolve(fullName);

        Assert.Equal(typeof(SampleEvent), resolved);
    }
}
