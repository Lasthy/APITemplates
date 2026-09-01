using Kernel.Events;

namespace Kernel.Testing.Fakes;

/// <summary>
/// A test fake implementing <see cref="IEventBus"/> that records all published events in-memory.
/// Enables assertions on published domain and integration events without side effects.
/// </summary>
public class FakeEventBus : IEventBus
{
    private readonly List<IEvent> _events = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets all events published through this event bus in chronological order.
    /// </summary>
    public IReadOnlyList<IEvent> PublishedEvents
    {
        get
        {
            lock (_lock)
            {
                return _events.ToList();
            }
        }
    }

    /// <summary>
    /// Records a published event in-memory.
    /// </summary>
    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        lock (_lock)
        {
            _events.Add(@event);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all published events of type <typeparamref name="T"/> in chronological order.
    /// </summary>
    public IReadOnlyList<T> GetPublishedEvents<T>() where T : IEvent
    {
        lock (_lock)
        {
            return _events.OfType<T>().ToList();
        }
    }

    /// <summary>
    /// Retrieves the most recently published event of type <typeparamref name="T"/>, or null if none was published.
    /// </summary>
    public T? GetLastPublishedEvent<T>() where T : IEvent
    {
        lock (_lock)
        {
            return _events.OfType<T>().LastOrDefault();
        }
    }

    /// <summary>
    /// Checks whether an event of type <typeparamref name="T"/> matching an optional predicate was published.
    /// </summary>
    public bool HasPublished<T>(Func<T, bool>? predicate = null) where T : IEvent
    {
        lock (_lock)
        {
            return predicate == null
                ? _events.OfType<T>().Any()
                : _events.OfType<T>().Any(predicate);
        }
    }

    /// <summary>
    /// Returns the number of published events of type <typeparamref name="T"/> matching an optional predicate.
    /// </summary>
    public int CountPublished<T>(Func<T, bool>? predicate = null) where T : IEvent
    {
        lock (_lock)
        {
            return predicate == null
                ? _events.OfType<T>().Count()
                : _events.OfType<T>().Count(predicate);
        }
    }

    /// <summary>
    /// Clears all recorded events.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}
