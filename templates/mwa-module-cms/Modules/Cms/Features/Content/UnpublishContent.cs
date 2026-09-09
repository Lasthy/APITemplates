using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to revert a published content item back to Draft status.
/// </summary>
public record UnpublishContent(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class UnpublishContentHandler : IRequestHandler<UnpublishContent, bool>
{
    private readonly IContentService _contentService;

    public UnpublishContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<bool>> HandleAsync(UnpublishContent request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.UnpublishAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
