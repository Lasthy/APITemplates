using Cms.ContentTypes;
using Cms.Contracts.Dtos;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.ContentTypes;

/// <summary>
/// Query to get detailed information and field schema for a content type by name.
/// </summary>
public record GetContentType(string Name) : IRequest<ContentTypeDetailDto>;

[Logging(Order = 1)]
public class GetContentTypeHandler : IRequestHandler<GetContentType, ContentTypeDetailDto>
{
    private readonly IContentTypeRegistry _typeRegistry;

    public GetContentTypeHandler(IContentTypeRegistry typeRegistry)
    {
        _typeRegistry = typeRegistry;
    }

    public Task<Result<ContentTypeDetailDto>> HandleAsync(GetContentType request, CancellationToken cancellationToken = default)
    {
        if (!_typeRegistry.TryGet(request.Name, out var descriptor) || descriptor == null)
        {
            return Task.FromResult(Result.Failure<ContentTypeDetailDto>(
                new KeyNotFoundException($"Content type '{request.Name}' not found.")));
        }

        return Task.FromResult(Result.Success(descriptor.ToDetailDto()));
    }
}
