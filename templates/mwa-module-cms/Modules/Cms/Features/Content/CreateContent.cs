using Cms.Contracts.Dtos;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Command to create a new content item.
/// </summary>
public record CreateContent(
    string ContentType,
    string Title,
    string? Body,
    string? Excerpt,
    string? FieldsJson,
    DefaultId AuthorId,
    IEnumerable<DefaultId>? TagIds = null) : IRequest<ContentItemDetailDto>;

[Logging(Order = 1)]
public class CreateContentHandler : IRequestHandler<CreateContent, ContentItemDetailDto>
{
    private readonly IContentService _contentService;

    public CreateContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<ContentItemDetailDto>> HandleAsync(CreateContent request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.CreateAsync(
            request.ContentType,
            request.Title,
            request.Body,
            request.Excerpt,
            request.FieldsJson,
            request.AuthorId,
            request.TagIds,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<ContentItemDetailDto>(result.Error!);
        }

        // Fetch populated with navigations
        var detailResult = await _contentService.GetByIdAsync(result.Value!.Id, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.Success(result.Value!.ToDetailDto());
        }

        return Result.Success(detailResult.Value!.ToDetailDto());
    }
}
