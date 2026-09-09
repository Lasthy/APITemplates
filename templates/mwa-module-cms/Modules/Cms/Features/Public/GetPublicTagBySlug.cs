using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Public;

/// <summary>
/// Public query to get tag details along with its published content items.
/// </summary>
public record GetPublicTagBySlug(string Slug) : IRequest<PublicTagDetailDto>;

[Logging(Order = 1)]
public class GetPublicTagBySlugHandler : IRequestHandler<GetPublicTagBySlug, PublicTagDetailDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetPublicTagBySlugHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PublicTagDetailDto>> HandleAsync(GetPublicTagBySlug request, CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == request.Slug, cancellationToken);

        if (tag == null)
        {
            return Result.Failure<PublicTagDetailDto>(
                new KeyNotFoundException($"Tag '{request.Slug}' not found."));
        }

        var contents = await _dbContext.ContentItemTags
            .AsNoTracking()
            .Where(cit => cit.ContentTagId == tag.Id && cit.ContentItem.Status == ContentStatus.Published && cit.ContentItem.DeletedAt == null)
            .Select(cit => cit.ContentItem)
            .OrderBy(ci => ci.SortOrder)
            .ThenByDescending(ci => ci.PublishedAt ?? ci.CreatedAt)
            .Select(ci => ci.ToDto())
            .ToListAsync(cancellationToken);

        return Result.Success(new PublicTagDetailDto(
            tag.Id,
            tag.Name,
            tag.Slug,
            tag.Description,
            contents));
    }
}
