using Cms.ContentTypes;
using Cms.Contracts.Dtos;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.ContentTypes;

/// <summary>
/// Query to list all registered content types.
/// </summary>
public record GetContentTypes : IRequest<IReadOnlyList<ContentTypeDto>>;

[Logging(Order = 1)]
public class GetContentTypesHandler : IRequestHandler<GetContentTypes, IReadOnlyList<ContentTypeDto>>
{
    private readonly IContentTypeRegistry _typeRegistry;

    public GetContentTypesHandler(IContentTypeRegistry typeRegistry)
    {
        _typeRegistry = typeRegistry;
    }

    public Task<Result<IReadOnlyList<ContentTypeDto>>> HandleAsync(GetContentTypes request, CancellationToken cancellationToken = default)
    {
        var list = _typeRegistry.GetAll()
            .Select(d => d.ToDto())
            .ToList();

        return Task.FromResult(Result.Success<IReadOnlyList<ContentTypeDto>>(list));
    }
}
