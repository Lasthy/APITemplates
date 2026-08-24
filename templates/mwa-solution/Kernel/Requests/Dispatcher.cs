using Kernel.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Requests;

public class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<Result<TResponse>> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            return Task.FromResult(Result.Failure<TResponse>(
                new InvalidOperationException($"No handler registered for request type '{requestType.Name}' with response '{typeof(TResponse).Name}'.")
            ));
        }

        var method = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync))!;

        // 1. Discover handler-level interceptor attributes
        var handlerConcreteType = handler.GetType();
        var interceptorAttributes = handlerConcreteType
            .GetCustomAttributes(typeof(RequestInterceptorAttribute), inherit: true)
            .Cast<RequestInterceptorAttribute>()
            .OrderBy(attr => attr.Order)
            .ToList();

        // 2. Build interceptor pipeline
        var interceptors = new List<IRequestInterceptor>();

        // Include any global interceptors registered in DI container
        var globalInterceptors = _serviceProvider.GetServices<IRequestInterceptor>();
        interceptors.AddRange(globalInterceptors);

        // Add handler-specific interceptors ordered by Order
        foreach (var attr in interceptorAttributes)
        {
            interceptors.Add(attr.CreateInterceptor(_serviceProvider));
        }

        // 3. Construct execution pipeline
        RequestHandlerDelegate<TResponse> next = () =>
        {
            try
            {
                var task = (Task<Result<TResponse>>)method.Invoke(handler, [request, cancellationToken])!;
                return task;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                return Task.FromResult(Result.Failure<TResponse>(inner));
            }
        };

        for (int i = interceptors.Count - 1; i >= 0; i--)
        {
            var interceptor = interceptors[i];
            var nextDelegate = next;
            next = () => interceptor.InterceptAsync(request, nextDelegate, cancellationToken);
        }

        return next();
    }
}
