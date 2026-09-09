using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to replace the tags assigned to a content item.
/// </summary>
public record SetContentTags(DefaultId ContentId, IEnumerable<DefaultId> TagIds) : IRequest<bool>;

[Logging(Order = 1)]
public class SetContentTagsHandler : IRequestHandler<SetContentTags, bool>
{
    private readonly IContentService _contentService;

    public SetContentTagsHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<bool>> HandleAsync(SetContentTags request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.SetTagsAsync(request.ContentId, request.TagIds, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
