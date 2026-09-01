using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;

namespace Kernel.Authorization;

/// <summary>
/// Interceptor that evaluates an <see cref="IAuthorizationPolicy"/> before executing a request handler.
/// </summary>
public class AuthorizationInterceptor : IRequestInterceptor
{
    private readonly AuthorizationPolicyRegistry _registry;
    private readonly IScopeContext _scopeContext;
    private readonly string _policyName;
    private readonly IServiceProvider? _serviceProvider;

    public AuthorizationInterceptor(
        AuthorizationPolicyRegistry registry,
        IScopeContext scopeContext,
        string policyName,
        IServiceProvider? serviceProvider = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _scopeContext = scopeContext ?? throw new ArgumentNullException(nameof(scopeContext));
        _policyName = policyName ?? throw new ArgumentNullException(nameof(policyName));
        _serviceProvider = serviceProvider;
    }

    public async Task<Result<TResponse>> InterceptAsync<TResponse>(
        IRequest<TResponse> request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var policy = _registry.GetPolicy(_policyName, _serviceProvider);
        if (policy == null)
        {
            return Result.Failure<TResponse>(
                new InvalidOperationException($"Authorization policy '{_policyName}' not found."));
        }

        var authResult = await policy.AuthorizeAsync(_scopeContext, request, cancellationToken);
        if (authResult.IsFailure)
        {
            var error = authResult.Error ?? new UnauthorizedAccessException(
                authResult.Messages.FirstOrDefault() ?? "Authorization failed.");
            return Result.Failure<TResponse>(error);
        }

        return await next();
    }
}
