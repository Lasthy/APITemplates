using Cms.Contracts.Dtos;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to restore a content item to a specific historical revision.
/// </summary>
public record RestoreRevision(DefaultId ContentId, int RevisionNumber) : IRequest<ContentItemDetailDto>;

[Logging(Order = 1)]
public class RestoreRevisionHandler : IRequestHandler<RestoreRevision, ContentItemDetailDto>
{
    private readonly IContentService _contentService;

    public RestoreRevisionHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<ContentItemDetailDto>> HandleAsync(RestoreRevision request, CancellationToken cancellationToken = default)
    {
        var restoreResult = await _contentService.RestoreRevisionAsync(
            request.ContentId,
            request.RevisionNumber,
            cancellationToken);

        if (restoreResult.IsFailure)
        {
            return Result.Failure<ContentItemDetailDto>(restoreResult.Error!);
        }

        var detailResult = await _contentService.GetByIdAsync(request.ContentId, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.Failure<ContentItemDetailDto>(detailResult.Error!);
        }

        return Result.Success(detailResult.Value!.ToDetailDto());
    }
}
