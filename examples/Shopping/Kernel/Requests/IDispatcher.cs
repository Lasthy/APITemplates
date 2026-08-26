using Kernel.Common;

namespace Kernel.Requests;

public interface IDispatcher
{
    Task<Result<TResponse>> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
