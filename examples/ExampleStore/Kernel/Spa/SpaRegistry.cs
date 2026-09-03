using System.Collections.Concurrent;

namespace Kernel.Spa;

/// <summary>
/// Central registry aggregating all Single Page Applications (SPAs) registered across the solution
/// (both Host-level and Module-defined SPAs).
/// </summary>
public class SpaRegistry
{
    private readonly ConcurrentDictionary<string, SpaAppOptions> _apps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all registered SPA applications.
    /// </summary>
    public IReadOnlyCollection<SpaAppOptions> Apps => _apps.Values.ToList().AsReadOnly();

    /// <summary>
    /// Registers an SPA application into the registry.
    /// </summary>
    public void RegisterApp(SpaAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _apps[options.Name] = options;
    }

    /// <summary>
    /// Registers multiple SPA applications into the registry.
    /// </summary>
    public void RegisterApps(IEnumerable<SpaAppOptions> apps)
    {
        ArgumentNullException.ThrowIfNull(apps);
        foreach (var app in apps)
        {
            RegisterApp(app);
        }
    }

    /// <summary>
    /// Tries to get a registered SPA application by its unique name.
    /// </summary>
    public bool TryGetApp(string name, out SpaAppOptions? app)
    {
        return _apps.TryGetValue(name, out app);
    }

    /// <summary>
    /// Gets all SPA applications owned by a specific module or host.
    /// </summary>
    public IEnumerable<SpaAppOptions> GetAppsByOwner(string owner)
    {
        return _apps.Values.Where(a => string.Equals(a.Owner, owner, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns all enabled SPA applications sorted by <see cref="SpaAppOptions.BasePath"/> length descending.
    /// This ensures deeper/nested base paths (e.g. <c>/auth/admin</c>) are evaluated before parent paths (<c>/auth</c>)
    /// and root paths (<c>/</c>).
    /// </summary>
    public IReadOnlyList<SpaAppOptions> GetOrderedApps()
    {
        return _apps.Values
            .Where(a => a.Enabled)
            .OrderByDescending(a => a.BasePath.Length)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Clears all registered SPA applications. Useful for testing.
    /// </summary>
    public void Clear()
    {
        _apps.Clear();
    }
}
