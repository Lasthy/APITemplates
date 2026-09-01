using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to inspect registered services at build time.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Retrieves a registered singleton instance from the service collection if it was registered with an implementation instance.
    /// </summary>
    /// <typeparam name="T">The type of singleton service to resolve.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The registered instance if found; otherwise, <c>null</c>.</returns>
    public static T? GetRegisteredSingleton<T>(this IServiceCollection services) where T : class
    {
        return services
            .Where(d => d.ServiceType == typeof(T))
            .Select(d => d.ImplementationInstance as T)
            .FirstOrDefault();
    }
}
