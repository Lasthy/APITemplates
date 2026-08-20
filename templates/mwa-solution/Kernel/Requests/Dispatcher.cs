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
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            return Task.FromResult(Result.Failure<TResponse>(new InvalidOperationException($"No handler registered for request type {requestType.Name}")));
        }

        var method = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync))!;
        
        // Build interceptor pipeline
        var interceptors = _serviceProvider.GetServices<IRequestInterceptor>().ToList();
        
        RequestHandlerDelegate<TResponse> next = () => (Task<Result<TResponse>>)method.Invoke(handler, [request, cancellationToken])!;

        for (int i = interceptors.Count - 1; i >= 0; i--)
        {
            var interceptor = interceptors[i];
            var nextDelegate = next;
            next = () => interceptor.InterceptAsync(request, nextDelegate, cancellationToken);
        }

        return next();
    }
}
