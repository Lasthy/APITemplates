using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Authorization;

/// <summary>
/// Singleton registry of all named authorization policies.
/// Policies can be registered manually, discovered from DI, or resolved dynamically by type.
/// </summary>
public class AuthorizationPolicyRegistry
{
    private readonly ConcurrentDictionary<string, IAuthorizationPolicy> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.OrdinalIgnoreCase);

    public AuthorizationPolicyRegistry()
    {
    }

    public AuthorizationPolicyRegistry(IEnumerable<IAuthorizationPolicy> policies)
    {
        if (policies != null)
        {
            foreach (var policy in policies)
            {
                Register(policy);
            }
        }
    }

    /// <summary>
    /// Registers a policy instance.
    /// </summary>
    public void Register(IAuthorizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _instances[policy.Name] = policy;
    }

    /// <summary>
    /// Registers a policy type to be instantiated via DI when evaluated.
    /// </summary>
    public void Register<TPolicy>() where TPolicy : IAuthorizationPolicy
    {
        Register(TPolicy.PolicyName, typeof(TPolicy));
    }

    /// <summary>
    /// Registers a policy type to be instantiated via DI when evaluated.
    /// </summary>
    public void Register(Type policyType)
    {
        ArgumentNullException.ThrowIfNull(policyType);
        if (!typeof(IAuthorizationPolicy).IsAssignableFrom(policyType))
        {
            throw new ArgumentException(
                $"Type '{policyType.FullName}' must implement '{nameof(IAuthorizationPolicy)}'.",
                nameof(policyType));
        }

        // Try reading static PolicyName property directly
        var staticProp = policyType.GetProperty(nameof(IAuthorizationPolicy.PolicyName), BindingFlags.Public | BindingFlags.Static);
        if (staticProp != null)
        {
            var staticName = staticProp.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(staticName))
            {
                _types[staticName] = policyType;
            }
        }

        // If parameterless constructor is present, instantiate once to cache instance and Name
        if (policyType.GetConstructor(Type.EmptyTypes) != null)
        {
            var tempInstance = (IAuthorizationPolicy)Activator.CreateInstance(policyType)!;
            _instances[tempInstance.Name] = tempInstance;
            _types[tempInstance.Name] = policyType;
        }
    }

    /// <summary>
    /// Registers a policy name associated with a specific implementation type.
    /// </summary>
    public void Register(string policyName, Type policyType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(policyType);
        if (!typeof(IAuthorizationPolicy).IsAssignableFrom(policyType))
        {
            throw new ArgumentException(
                $"Type '{policyType.FullName}' must implement '{nameof(IAuthorizationPolicy)}'.",
                nameof(policyType));
        }

        _types[policyName] = policyType;
    }

    /// <summary>
    /// Resolves the named policy, optionally using the provided service provider for scoped/transient policies.
    /// </summary>
    public IAuthorizationPolicy? GetPolicy(string name, IServiceProvider? serviceProvider = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // 1. Direct instance lookup
        if (_instances.TryGetValue(name, out var policy))
        {
            return policy;
        }

        // 2. Service provider resolution
        if (serviceProvider != null)
        {
            // Try registered type
            if (_types.TryGetValue(name, out var policyType))
            {
                var resolved = ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, policyType) as IAuthorizationPolicy;
                if (resolved != null) return resolved;
            }

            // Try all IAuthorizationPolicy in DI container
            var diPolicies = serviceProvider.GetServices<IAuthorizationPolicy>();
            foreach (var diPolicy in diPolicies)
            {
                _instances.TryAdd(diPolicy.Name, diPolicy);
                if (string.Equals(diPolicy.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return diPolicy;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the named policy from registered instances.
    /// </summary>
    public IAuthorizationPolicy? GetPolicy(string name) => GetPolicy(name, null);

    /// <summary>
    /// Returns all registered policy names.
    /// </summary>
    public IReadOnlyCollection<string> GetPolicyNames()
    {
        return _instances.Keys.Union(_types.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
