using System.Reflection;

namespace Kernel.Events;

/// <summary>
/// Registry used to resolve event CLR types when dealing with Inbox/Outbox
/// </summary>
public interface IEventTypeRegistry
{
    /// <summary>
    /// Registers an event type by its full name
    /// </summary>
    void Register<T>() where T : IEvent;

    /// <summary>
    /// Resolves a registered event type by its full name
    /// </summary>
    Type Resolve(string name);

    /// <summary>
    /// Registers all concrete event types found in the specified assembly.
    /// </summary>
    void RegisterFromAssembly(Assembly assembly);
}