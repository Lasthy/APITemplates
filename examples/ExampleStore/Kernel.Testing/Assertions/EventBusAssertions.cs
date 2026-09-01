using Kernel.Events;
using Kernel.Testing.Fakes;
using Xunit;

namespace Kernel.Testing.Assertions;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeEventBus"/>.
/// </summary>
public static class EventBusAssertions
{
    /// <summary>
    /// Asserts that at least one event of type <typeparamref name="TEvent"/> was published.
    /// </summary>
    /// <returns>The first matching published event.</returns>
    public static TEvent ShouldHavePublished<TEvent>(this FakeEventBus bus, string? userMessage = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(bus);
        var events = bus.GetPublishedEvents<TEvent>();
        Assert.True(events.Count > 0, userMessage ?? $"Expected event of type '{typeof(TEvent).Name}' to be published, but none was recorded.");
        return events[0];
    }

    /// <summary>
    /// Asserts that an event of type <typeparamref name="TEvent"/> matching the given predicate was published.
    /// </summary>
    /// <returns>The first matching published event.</returns>
    public static TEvent ShouldHavePublished<TEvent>(
        this FakeEventBus bus,
        Func<TEvent, bool> predicate,
        string? userMessage = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(predicate);

        var events = bus.GetPublishedEvents<TEvent>();
        var matching = events.FirstOrDefault(predicate);
        Assert.NotNull(matching);
        return matching;
    }

    /// <summary>
    /// Asserts that exactly <paramref name="expectedCount"/> events of type <typeparamref name="TEvent"/> were published.
    /// </summary>
    public static IReadOnlyList<TEvent> ShouldHavePublished<TEvent>(
        this FakeEventBus bus,
        int expectedCount,
        string? userMessage = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(bus);
        var events = bus.GetPublishedEvents<TEvent>();
        Assert.True(
            events.Count == expectedCount,
            userMessage ?? $"Expected {expectedCount} event(s) of type '{typeof(TEvent).Name}', but found {events.Count}.");
        return events;
    }

    /// <summary>
    /// Asserts that no events of type <typeparamref name="TEvent"/> were published.
    /// </summary>
    public static void ShouldNotHavePublished<TEvent>(this FakeEventBus bus, string? userMessage = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(bus);
        var events = bus.GetPublishedEvents<TEvent>();
        Assert.True(
            events.Count == 0,
            userMessage ?? $"Expected no events of type '{typeof(TEvent).Name}' to be published, but found {events.Count}.");
    }

    /// <summary>
    /// Asserts that no events of type <typeparamref name="TEvent"/> matching the given predicate were published.
    /// </summary>
    public static void ShouldNotHavePublished<TEvent>(
        this FakeEventBus bus,
        Func<TEvent, bool> predicate,
        string? userMessage = null)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(predicate);

        var events = bus.GetPublishedEvents<TEvent>();
        var matching = events.Where(predicate).ToList();
        Assert.True(
            matching.Count == 0,
            userMessage ?? $"Expected no matching events of type '{typeof(TEvent).Name}', but found {matching.Count}.");
    }

    /// <summary>
    /// Asserts that no events of any type were published.
    /// </summary>
    public static void ShouldBeEmpty(this FakeEventBus bus, string? userMessage = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        Assert.True(
            bus.PublishedEvents.Count == 0,
            userMessage ?? $"Expected event bus to be empty, but found {bus.PublishedEvents.Count} event(s).");
    }
}
