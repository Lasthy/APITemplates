using Microsoft.EntityFrameworkCore;

namespace Kernel.Persistence;

/// <summary>
/// Registry of all DbContext types registered across the host and active modules.
/// Used during application startup to automatically ensure databases and schemas are created.
/// </summary>
public class DbContextTypeRegistry
{
    private readonly HashSet<Type> _types = [];

    public IReadOnlyCollection<Type> Types => _types;

    public void Register<TContext>() where TContext : DbContext
    {
        _types.Add(typeof(TContext));
    }

    public void Register(Type contextType)
    {
        if (contextType != null && typeof(DbContext).IsAssignableFrom(contextType) && contextType != typeof(DbContext))
        {
            _types.Add(contextType);
        }
    }
}
