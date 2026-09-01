using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Validation.Extensions;

/// <summary>
/// Extension methods for registering validation services and auto-discovering <see cref="IValidator{T}"/> implementations.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Scans the specified assemblies for all implementations of <see cref="IValidator{T}"/>
    /// and registers them into the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValidation(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddTransient<ValidationInterceptor>();

        foreach (var assembly in assemblies)
        {
            services.RegisterValidatorsFromAssembly(assembly);
        }

        return services;
    }

    /// <summary>
    /// Scans an assembly for all concrete implementations of <see cref="IValidator{T}"/>
    /// and registers them as scoped services under both their concrete type and all implemented validator interfaces.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var validatorOpenType = typeof(IValidator<>);

        var types = assembly.GetTypes()
            .Where(t => !t.IsAbstract &&
                        !t.IsInterface &&
                        !t.IsGenericTypeDefinition)
            .ToList();

        foreach (var type in types)
        {
            var validatorInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorOpenType)
                .ToList();

            if (validatorInterfaces.Count > 0)
            {
                services.AddScoped(type);
                foreach (var validatorInterface in validatorInterfaces)
                {
                    services.AddScoped(validatorInterface, type);
                }
            }
        }

        return services;
    }
}
