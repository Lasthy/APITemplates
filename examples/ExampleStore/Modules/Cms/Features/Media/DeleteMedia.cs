using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Media;

/// <summary>
/// Command to delete a media record and remove the file from storage.
/// </summary>
public record DeleteMedia(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class DeleteMediaHandler : IRequestHandler<DeleteMedia, bool>
{
    private readonly IContentMediaService _mediaService;

    public DeleteMediaHandler(IContentMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    public async Task<Result<bool>> HandleAsync(DeleteMedia request, CancellationToken cancellationToken = default)
    {
        var result = await _mediaService.DeleteAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
