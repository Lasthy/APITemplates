using Kernel.Context;
using Kernel.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Authorization;

/// <summary>
/// Applies policy-based authorization to a request handler.
/// The dispatcher will resolve the named <see cref="IAuthorizationPolicy"/> and call
/// <see cref="IAuthorizationPolicy.AuthorizeAsync"/> before the handler executes.
/// Multiple policies can be applied to a handler (all must pass).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AuthorizeAttribute : RequestInterceptorAttribute
{
    /// <summary>
    /// The name of the policy to evaluate.
    /// </summary>
    public string PolicyName { get; }

    public AuthorizeAttribute(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        PolicyName = policyName;
        Order = -100; // Run before logging and transaction interceptors
    }

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<AuthorizationPolicyRegistry>();
        var scopeContext = serviceProvider.GetRequiredService<IScopeContext>();
        return new AuthorizationInterceptor(registry, scopeContext, PolicyName, serviceProvider);
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="AuthorizeAttribute"/> that resolves the policy name
/// directly from <typeparamref name="TPolicy"/>.
/// </summary>
/// <typeparam name="TPolicy">The policy type implementing <see cref="IAuthorizationPolicy"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AuthorizeAttribute<TPolicy> : AuthorizeAttribute where TPolicy : IAuthorizationPolicy
{
    public AuthorizeAttribute() : base(TPolicy.PolicyName)
    {
    }
}
