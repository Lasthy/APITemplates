using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to transition a content item to Archived status.
/// </summary>
public record ArchiveContent(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class ArchiveContentHandler : IRequestHandler<ArchiveContent, bool>
{
    private readonly IContentService _contentService;

    public ArchiveContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<bool>> HandleAsync(ArchiveContent request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.ArchiveAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
