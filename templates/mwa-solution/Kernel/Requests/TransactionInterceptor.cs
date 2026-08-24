using System.Transactions;
using Kernel.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Requests;

/// <summary>
/// Wraps a request handler execution in a <see cref="TransactionScope"/>.
/// Commits the transaction if the handler returns a successful <see cref="Result{T}"/>,
/// or rolls back if a failure result or exception occurs.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class TransactionAttribute : RequestInterceptorAttribute
{
    /// <summary>
    /// Transaction isolation level. Defaults to <see cref="IsolationLevel.ReadCommitted"/>.
    /// </summary>
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    /// <summary>
    /// Transaction timeout in seconds. Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Transaction scope option. Defaults to <see cref="TransactionScopeOption.Required"/>.
    /// </summary>
    public TransactionScopeOption ScopeOption { get; set; } = TransactionScopeOption.Required;

    public override IRequestInterceptor CreateInterceptor(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILogger<TransactionInterceptor>>();
        return new TransactionInterceptor(logger, IsolationLevel, TimeoutSeconds, ScopeOption);
    }
}

/// <summary>
/// Interceptor that wraps request execution in a <see cref="TransactionScope"/>.
/// Commits the transaction if the handler returns a successful <see cref="Result{T}"/>,
/// or rolls back if a failure result or exception occurs.
/// </summary>
public class TransactionInterceptor : IRequestInterceptor
{
    private readonly ILogger<TransactionInterceptor>? _logger;
    private readonly IsolationLevel _isolationLevel;
    private readonly int _timeoutSeconds;
    private readonly TransactionScopeOption _scopeOption;

    public TransactionInterceptor(
        ILogger<TransactionInterceptor>? logger = null,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        int timeoutSeconds = 30,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required)
    {
        _logger = logger;
        _isolationLevel = isolationLevel;
        _timeoutSeconds = timeoutSeconds;
        _scopeOption = scopeOption;
    }

    public async Task<Result<TResponse>> InterceptAsync<TResponse>(
        IRequest<TResponse> request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var requestName = request.GetType().Name;
        var transactionOptions = new TransactionOptions
        {
            IsolationLevel = _isolationLevel,
            Timeout = TimeSpan.FromSeconds(_timeoutSeconds)
        };

        using var scope = new TransactionScope(
            _scopeOption,
            transactionOptions,
            TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            var result = await next();

            if (result.IsSuccess)
            {
                scope.Complete();
                _logger?.LogDebug("Transaction committed for request {RequestName}.", requestName);
            }
            else
            {
                var errorMessage = result.Error?.Message ?? string.Join("; ", result.Messages);
                _logger?.LogWarning(
                    "Transaction rolled back for request {RequestName} due to failed result: {ErrorMessage}",
                    requestName,
                    errorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Transaction rolled back for request {RequestName} due to an unhandled exception: {ErrorMessage}",
                requestName,
                ex.Message);

            return Result.Failure<TResponse>(ex);
        }
    }
}
