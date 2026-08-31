using Kernel.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Authorization;

/// <summary>
/// <see cref="IEndpointFilter"/> that evaluates a named <see cref="IAuthorizationPolicy"/>.
/// Use with <c>.AddEndpointFilter()</c> or <c>.RequireAuthorizationPolicy()</c> on Minimal API routes and route groups.
/// </summary>
public class AuthorizationEndpointFilter : IEndpointFilter
{
    private readonly string _policyName;

    public AuthorizationEndpointFilter(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        _policyName = policyName;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var registry = services.GetRequiredService<AuthorizationPolicyRegistry>();
        var scopeContext = services.GetRequiredService<IScopeContext>();

        var policy = registry.GetPolicy(_policyName, services);
        if (policy == null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Policy Not Found",
                detail: $"Authorization policy '{_policyName}' not found.");
        }

        var authResult = await policy.AuthorizeAsync(scopeContext, context.Arguments, context.HttpContext.RequestAborted);
        if (authResult.IsFailure)
        {
            if (!scopeContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            return Results.Forbid();
        }

        return await next(context);
    }
}
