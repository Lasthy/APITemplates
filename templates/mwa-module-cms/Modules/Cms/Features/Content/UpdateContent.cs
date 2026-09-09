using Cms.Contracts.Dtos;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to update an existing content item, incrementing the revision number and recording a revision snapshot.
/// </summary>
public record UpdateContent(
    DefaultId Id,
    string? Title = null,
    string? Body = null,
    string? Excerpt = null,
    string? FieldsJson = null,
    string? MetadataJson = null,
    string? ChangeNote = null) : IRequest<ContentItemDetailDto>;

[Logging(Order = 1)]
public class UpdateContentHandler : IRequestHandler<UpdateContent, ContentItemDetailDto>
{
    private readonly IContentService _contentService;

    public UpdateContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<ContentItemDetailDto>> HandleAsync(UpdateContent request, CancellationToken cancellationToken = default)
    {
        var updateResult = await _contentService.UpdateAsync(
            request.Id,
            request.Title,
            request.Body,
            request.Excerpt,
            request.FieldsJson,
            request.MetadataJson,
            request.ChangeNote,
            cancellationToken);

        if (updateResult.IsFailure)
        {
            return Result.Failure<ContentItemDetailDto>(updateResult.Error!);
        }

        var detailResult = await _contentService.GetByIdAsync(request.Id, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.Failure<ContentItemDetailDto>(detailResult.Error!);
        }

        return Result.Success(detailResult.Value!.ToDetailDto());
    }
}
