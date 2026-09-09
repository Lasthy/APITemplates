using Cms.Contracts.Dtos;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Media;

/// <summary>
/// Query to list all media attachments for a specific content item.
/// </summary>
public record GetContentMedia(DefaultId ContentId) : IRequest<IReadOnlyList<ContentMediaDto>>;

[Logging(Order = 1)]
public class GetContentMediaHandler : IRequestHandler<GetContentMedia, IReadOnlyList<ContentMediaDto>>
{
    private readonly IContentMediaService _mediaService;

    public GetContentMediaHandler(IContentMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    public async Task<Result<IReadOnlyList<ContentMediaDto>>> HandleAsync(GetContentMedia request, CancellationToken cancellationToken = default)
    {
        var result = await _mediaService.GetByContentItemAsync(request.ContentId, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ContentMediaDto>>(result.Error!);
        }

        var list = result.Value!.Select(m => m.ToDto()).ToList();
        return Result.Success<IReadOnlyList<ContentMediaDto>>(list);
    }
}
