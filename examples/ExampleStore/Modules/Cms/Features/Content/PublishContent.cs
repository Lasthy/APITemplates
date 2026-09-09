using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to publish a draft or archived content item.
/// </summary>
public record PublishContent(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class PublishContentHandler : IRequestHandler<PublishContent, bool>
{
    private readonly IContentService _contentService;

    public PublishContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<bool>> HandleAsync(PublishContent request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.PublishAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
