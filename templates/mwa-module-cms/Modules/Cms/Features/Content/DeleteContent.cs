using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to soft-delete a content item.
/// </summary>
public record DeleteContent(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class DeleteContentHandler : IRequestHandler<DeleteContent, bool>
{
    private readonly IContentService _contentService;

    public DeleteContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<bool>> HandleAsync(DeleteContent request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.DeleteAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
