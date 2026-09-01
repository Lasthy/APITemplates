using System.Reflection;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Validation;

/// <summary>
/// Interceptor that evaluates all registered <see cref="IValidator{T}"/> validators for a request.
/// Short-circuits with a failed <see cref="Result{TResponse}"/> containing a <see cref="ValidationException"/>
/// and formatted error messages if validation fails.
/// </summary>
public class ValidationInterceptor : IRequestInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<Result<TResponse>> InterceptAsync<TResponse>(
        IRequest<TResponse> request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
        var validators = _serviceProvider.GetServices(validatorType);

        var errors = new List<ValidationError>();

        foreach (var validator in validators)
        {
            if (validator == null) continue;

            var method = validatorType.GetMethod(nameof(IValidator<object>.Validate));
            if (method != null)
            {
                try
                {
                    var validationResult = method.Invoke(validator, [request]) as ValidationResult;
                    if (validationResult != null && !validationResult.IsValid)
                    {
                        errors.AddRange(validationResult.Errors);
                    }
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    return Result.Failure<TResponse>(ex.InnerException);
                }
                catch (Exception ex)
                {
                    return Result.Failure<TResponse>(ex);
                }
            }
        }

        if (errors.Count > 0)
        {
            var combinedResult = ValidationResult.Failure(errors);
            var exception = new ValidationException(combinedResult);
            var messages = errors
                .Select(e => string.IsNullOrWhiteSpace(e.Field) ? e.Message : $"{e.Field}: {e.Message}")
                .ToArray();

            return Result.Failure<TResponse>(exception, messages);
        }

        return await next();
    }
}
