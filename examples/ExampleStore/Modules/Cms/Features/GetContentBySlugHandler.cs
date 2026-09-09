using Cms.Contracts.Dtos;
using Cms.Contracts.Requests;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features;

/// <summary>
/// Cross-module handler for <see cref="GetContentBySlug"/>.
/// Allows other modules to query content item details by content type and slug.
/// </summary>
[Logging(Order = 1)]
public class GetContentBySlugHandler : IRequestHandler<GetContentBySlug, ContentItemDetailDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetContentBySlugHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentItemDetailDto>> HandleAsync(GetContentBySlug request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ContentItems
            .AsNoTracking()
            .Include(ci => ci.Tags)
                .ThenInclude(t => t.ContentTag)
            .Include(ci => ci.Media)
            .FirstOrDefaultAsync(ci =>
                ci.ContentType == request.ContentType &&
                ci.Slug == request.Slug &&
                ci.DeletedAt == null, cancellationToken);

        if (item == null)
        {
            return Result.Failure<ContentItemDetailDto>(
                new KeyNotFoundException($"Content item '{request.ContentType}:{request.Slug}' not found."));
        }

        return Result.Success(item.ToDetailDto());
    }
}
