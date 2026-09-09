using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Public;

/// <summary>
/// Public query to list all tags with published content counts.
/// </summary>
public record GetPublicTags : IRequest<IReadOnlyList<PublicTagDto>>;

[Logging(Order = 1)]
public class GetPublicTagsHandler : IRequestHandler<GetPublicTags, IReadOnlyList<PublicTagDto>>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetPublicTagsHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<PublicTagDto>>> HandleAsync(GetPublicTags request, CancellationToken cancellationToken = default)
    {
        var tags = await _dbContext.Tags
            .AsNoTracking()
            .Select(t => new PublicTagDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Description,
                t.ContentItems.Count(cit => cit.ContentItem.Status == ContentStatus.Published && cit.ContentItem.DeletedAt == null)))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PublicTagDto>>(tags);
    }
}
