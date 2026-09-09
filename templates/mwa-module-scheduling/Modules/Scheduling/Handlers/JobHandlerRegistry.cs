using System.Collections.Concurrent;
using System.Reflection;

namespace Scheduling.Handlers;

/// <summary>
/// Registry mapping handler type names to their concrete CLR types.
/// </summary>
public interface IJobHandlerRegistry
{
    void Register<T>() where T : class, IScheduledJobHandler;
    void Register(Type handlerType);
    void RegisterFromAssembly(Assembly assembly);
    Type? GetHandlerType(string typeName);
    bool TryGetHandlerType(string typeName, out Type? handlerType);
    IReadOnlyCollection<Type> GetRegisteredTypes();
}

/// <summary>
/// In-memory singleton registry for scheduled job handlers.
/// Allows looking up handler types by full name, simple name, or case-insensitively.
/// </summary>
public class JobHandlerRegistry : IJobHandlerRegistry
{
    private readonly ConcurrentDictionary<string, Type> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register<T>() where T : class, IScheduledJobHandler
    {
        Register(typeof(T));
    }

    public void Register(Type handlerType)
    {
        if (!typeof(IScheduledJobHandler).IsAssignableFrom(handlerType) || handlerType.IsAbstract || handlerType.IsInterface)
        {
            throw new ArgumentException($"Type '{handlerType.FullName}' must implement {nameof(IScheduledJobHandler)} and be concrete.", nameof(handlerType));
        }

        if (handlerType.FullName != null)
        {
            _handlers[handlerType.FullName] = handlerType;
        }

        _handlers[handlerType.Name] = handlerType;
    }

    public void RegisterFromAssembly(Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IScheduledJobHandler).IsAssignableFrom(t));

        foreach (var type in handlerTypes)
        {
            Register(type);
        }
    }

    public Type? GetHandlerType(string typeName)
    {
        TryGetHandlerType(typeName, out var handlerType);
        return handlerType;
    }

    public bool TryGetHandlerType(string typeName, out Type? handlerType)
    {
        return _handlers.TryGetValue(typeName, out handlerType);
    }

    public IReadOnlyCollection<Type> GetRegisteredTypes()
    {
        return _handlers.Values.Distinct().ToList().AsReadOnly();
    }
}
