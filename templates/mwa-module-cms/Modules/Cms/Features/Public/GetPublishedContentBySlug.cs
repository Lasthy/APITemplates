using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Public;

/// <summary>
/// Public query to get a single published content item by type and slug.
/// </summary>
public record GetPublishedContentBySlug(string ContentType, string Slug) : IRequest<ContentItemDetailDto>;

[Logging(Order = 1)]
public class GetPublishedContentBySlugHandler : IRequestHandler<GetPublishedContentBySlug, ContentItemDetailDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetPublishedContentBySlugHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentItemDetailDto>> HandleAsync(GetPublishedContentBySlug request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ContentItems
            .AsNoTracking()
            .Include(ci => ci.Tags)
                .ThenInclude(t => t.ContentTag)
            .Include(ci => ci.Media)
            .FirstOrDefaultAsync(ci =>
                ci.ContentType == request.ContentType &&
                ci.Slug == request.Slug &&
                ci.Status == ContentStatus.Published, cancellationToken);

        if (item == null)
        {
            return Result.Failure<ContentItemDetailDto>(
                new KeyNotFoundException($"Published content item '{request.ContentType}:{request.Slug}' not found."));
        }

        return Result.Success(item.ToDetailDto());
    }
}
