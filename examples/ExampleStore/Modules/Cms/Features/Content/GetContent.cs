using Cms.Contracts.Dtos;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Cms.Features.Content;

/// <summary>
/// Query to get full content item details by ID.
/// </summary>
public record GetContent(DefaultId Id) : IRequest<ContentItemDetailDto>;

[Logging(Order = 1)]
public class GetContentHandler : IRequestHandler<GetContent, ContentItemDetailDto>
{
    private readonly IContentService _contentService;

    public GetContentHandler(IContentService contentService)
    {
        _contentService = contentService;
    }

    public async Task<Result<ContentItemDetailDto>> HandleAsync(GetContent request, CancellationToken cancellationToken = default)
    {
        var result = await _contentService.GetByIdAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<ContentItemDetailDto>(result.Error!);
        }

        return Result.Success(result.Value!.ToDetailDto());
    }
}
