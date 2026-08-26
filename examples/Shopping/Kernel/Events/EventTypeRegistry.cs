using System.Collections.Concurrent;
using System.Reflection;

namespace Kernel.Events;

/// <summary>
/// The default event type registry implementation
/// </summary>
public sealed class EventTypeRegistry : IEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an event type by its full name.
    /// </summary>
    public void Register<T>() where T : IEvent
    {
        var type = typeof(T);

        Register(type);
    }

    /// <summary>
    /// Resolves a registered event type by its full name.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the event type is not registered.</exception>
    public Type Resolve(string name)
    {
        if (!_types.TryGetValue(name, out var type))
            throw new InvalidOperationException($"Unknown event type: {name}");

        return type;
    }

    /// <summary>
    /// Registers all concrete event types found in the specified assembly and its referenced assemblies.
    /// </summary>
    public void RegisterFromAssembly(Assembly assembly)
    {
        RegisterTypesFromAssembly(assembly);

        // Also scan referenced assemblies (e.g. companion .Contracts assemblies)
        var referencedAssemblyNames = assembly.GetReferencedAssemblies();
        foreach (var assemblyName in referencedAssemblyNames)
        {
            try
            {
                var refAssembly = Assembly.Load(assemblyName);
                RegisterTypesFromAssembly(refAssembly);
            }
            catch
            {
                // Ignore assemblies that cannot be loaded
            }
        }
    }

    private void RegisterTypesFromAssembly(Assembly assembly)
    {
        try
        {
            var types = assembly
                .GetTypes()
                .Where(t =>
                    typeof(IEvent).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    !t.IsInterface);

            foreach (var type in types)
                Register(type);
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var type in ex.Types.Where(t => t != null && typeof(IEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
            {
                Register(type!);
            }
        }
    }

    private void Register(Type type)
    {
        var typeName = type.FullName;

        if (string.IsNullOrWhiteSpace(typeName))
            throw new InvalidOperationException("Event type must have a full name to be registered.");

        if (_types.TryAdd(typeName, type))
            return;

        var existing = _types[typeName];

        if (existing != type)
            throw new InvalidOperationException($"Event type name collision detected for '{typeName}'. Existing: '{existing.AssemblyQualifiedName}', New: '{type.AssemblyQualifiedName}'.");
    }
}