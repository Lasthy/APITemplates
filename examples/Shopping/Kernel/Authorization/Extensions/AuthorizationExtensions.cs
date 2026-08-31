using System.Reflection;
using Kernel.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Authorization.Extensions;

/// <summary>
/// Extension methods for configuring authorization policies and endpoint filters.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the <see cref="AuthorizationPolicyRegistry"/>, built-in policies, and scans the specified assemblies
    /// for <see cref="IAuthorizationPolicy"/> implementations.
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services, params Assembly[] assemblies)
    {
        var registry = services
            .Where(d => d.ServiceType == typeof(AuthorizationPolicyRegistry))
            .Select(d => d.ImplementationInstance as AuthorizationPolicyRegistry)
            .FirstOrDefault();

        if (registry == null)
        {
            registry = new AuthorizationPolicyRegistry();
            services.AddSingleton(registry);
        }

        // Register built-in policies
        var authenticatedPolicy = new AuthenticatedPolicy();
        registry.Register(authenticatedPolicy);
        services.AddSingleton<IAuthorizationPolicy>(authenticatedPolicy);

        var systemRolePolicy = new RolePolicy(Roles.System, Roles.System);
        registry.Register(systemRolePolicy);
        services.AddSingleton<IAuthorizationPolicy>(systemRolePolicy);

        // Scan assemblies
        foreach (var assembly in assemblies)
        {
            services.RegisterAuthorizationPoliciesFromAssembly(assembly, registry);
        }

        return services;
    }

    /// <summary>
    /// Scans an assembly for <see cref="IAuthorizationPolicy"/> implementations, registers them in DI,
    /// and records them in the <see cref="AuthorizationPolicyRegistry"/>.
    /// </summary>
    public static IServiceCollection RegisterAuthorizationPoliciesFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        AuthorizationPolicyRegistry? registry = null)
    {
        var policyInterface = typeof(IAuthorizationPolicy);
        var policyTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract &&
                        !t.IsInterface &&
                        !t.IsGenericTypeDefinition &&
                        policyInterface.IsAssignableFrom(t) &&
                        t != typeof(RolePolicy) &&
                        t != typeof(AuthenticatedPolicy))
            .ToList();

        if (registry == null)
        {
            registry = services
                .Where(d => d.ServiceType == typeof(AuthorizationPolicyRegistry))
                .Select(d => d.ImplementationInstance as AuthorizationPolicyRegistry)
                .FirstOrDefault();
        }

        foreach (var policyType in policyTypes)
        {
            // Register as scoped so policies can inject scoped services (e.g. DbContext)
            services.AddScoped(policyType);
            services.AddScoped(policyInterface, policyType);

            if (registry != null)
            {
                registry.Register(policyType);
            }
        }

        return services;
    }

    /// <summary>
    /// Adds an <see cref="AuthorizationEndpointFilter"/> for the specified policy name to the endpoint.
    /// </summary>
    public static RouteHandlerBuilder RequireAuthorizationPolicy(this RouteHandlerBuilder builder, string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return builder.AddEndpointFilter(new AuthorizationEndpointFilter(policyName));
    }

    /// <summary>
    /// Adds an <see cref="AuthorizationEndpointFilter"/> for the specified policy type to the endpoint.
    /// </summary>
    public static RouteHandlerBuilder RequireAuthorizationPolicy<TPolicy>(this RouteHandlerBuilder builder)
        where TPolicy : IAuthorizationPolicy
    {
        return builder.RequireAuthorizationPolicy(TPolicy.PolicyName);
    }

    /// <summary>
    /// Adds an <see cref="AuthorizationEndpointFilter"/> for the specified policy name to the route group.
    /// </summary>
    public static RouteGroupBuilder RequireAuthorizationPolicy(this RouteGroupBuilder builder, string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        return builder.AddEndpointFilter(new AuthorizationEndpointFilter(policyName));
    }

    /// <summary>
    /// Adds an <see cref="AuthorizationEndpointFilter"/> for the specified policy type to the route group.
    /// </summary>
    public static RouteGroupBuilder RequireAuthorizationPolicy<TPolicy>(this RouteGroupBuilder builder)
        where TPolicy : IAuthorizationPolicy
    {
        return builder.RequireAuthorizationPolicy(TPolicy.PolicyName);
    }
}
