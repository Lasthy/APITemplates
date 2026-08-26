using Kernel.Common;

namespace Kernel.Requests;

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

public interface IRequestInterceptor
{
    Task<Result<TResponse>> InterceptAsync<TResponse>(IRequest<TResponse> request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
}
