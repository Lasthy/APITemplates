using Kernel.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Authorization;

/// <summary>
/// Action filter attribute for MVC controllers that evaluates a named <see cref="IAuthorizationPolicy"/>
/// before the action executes. Works alongside ASP.NET Core's built-in <c>[Authorize]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class AuthorizePolicyAttribute : ActionFilterAttribute
{
    /// <summary>
    /// The name of the policy to evaluate.
    /// </summary>
    public string PolicyName { get; }

    public AuthorizePolicyAttribute(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        PolicyName = policyName;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var registry = services.GetRequiredService<AuthorizationPolicyRegistry>();
        var scopeContext = services.GetRequiredService<IScopeContext>();

        var policy = registry.GetPolicy(PolicyName, services);
        if (policy == null)
        {
            context.Result = new ObjectResult(new
            {
                title = "Policy Not Found",
                detail = $"Authorization policy '{PolicyName}' not found."
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return;
        }

        var authResult = await policy.AuthorizeAsync(scopeContext, context.ActionArguments, context.HttpContext.RequestAborted);
        if (authResult.IsFailure)
        {
            if (!scopeContext.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
            }
            else
            {
                context.Result = new ForbidResult();
            }
            return;
        }

        await next();
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="AuthorizePolicyAttribute"/> that resolves the policy name
/// directly from <typeparamref name="TPolicy"/>.
/// </summary>
/// <typeparam name="TPolicy">The policy type implementing <see cref="IAuthorizationPolicy"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class AuthorizePolicyAttribute<TPolicy> : AuthorizePolicyAttribute where TPolicy : IAuthorizationPolicy
{
    public AuthorizePolicyAttribute() : base(TPolicy.PolicyName)
    {
    }
}

/// <summary>
/// Standalone <see cref="IAsyncActionFilter"/> implementation evaluating an <see cref="IAuthorizationPolicy"/>.
/// </summary>
public class AuthorizationActionFilter : IAsyncActionFilter
{
    private readonly string _policyName;

    public AuthorizationActionFilter(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        _policyName = policyName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var registry = services.GetRequiredService<AuthorizationPolicyRegistry>();
        var scopeContext = services.GetRequiredService<IScopeContext>();

        var policy = registry.GetPolicy(_policyName, services);
        if (policy == null)
        {
            context.Result = new ObjectResult(new
            {
                title = "Policy Not Found",
                detail = $"Authorization policy '{_policyName}' not found."
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return;
        }

        var authResult = await policy.AuthorizeAsync(scopeContext, context.ActionArguments, context.HttpContext.RequestAborted);
        if (authResult.IsFailure)
        {
            if (!scopeContext.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
            }
            else
            {
                context.Result = new ForbidResult();
            }
            return;
        }

        await next();
    }
}
