using Cms.Contracts.Dtos;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Media;

/// <summary>
/// Command to upload a media file, optionally attaching it to a content item.
/// </summary>
public record UploadMedia(
    DefaultId? ContentItemId,
    Stream FileStream,
    string FileName,
    string MimeType,
    string? AltText = null) : IRequest<ContentMediaDto>;

[Logging(Order = 1)]
public class UploadMediaHandler : IRequestHandler<UploadMedia, ContentMediaDto>
{
    private readonly IContentMediaService _mediaService;

    public UploadMediaHandler(IContentMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    public async Task<Result<ContentMediaDto>> HandleAsync(UploadMedia request, CancellationToken cancellationToken = default)
    {
        var result = await _mediaService.UploadAsync(
            request.ContentItemId,
            request.FileStream,
            request.FileName,
            request.MimeType,
            request.AltText,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<ContentMediaDto>(result.Error!);
        }

        return Result.Success(result.Value!.ToDto());
    }
}
