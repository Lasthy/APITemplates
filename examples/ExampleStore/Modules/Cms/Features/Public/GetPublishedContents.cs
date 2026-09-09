using Cms.Contracts.Constants;
using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Public;

/// <summary>
/// Public query to list published content items with optional filters and pagination.
/// </summary>
public record GetPublishedContents(
    string? ContentType = null,
    string? Tag = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<ContentItemDto>>;

[Logging(Order = 1)]
public class GetPublishedContentsHandler : IRequestHandler<GetPublishedContents, PaginatedResult<ContentItemDto>>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetPublishedContentsHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<ContentItemDto>>> HandleAsync(GetPublishedContents request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ContentItems
            .AsNoTracking()
            .Where(ci => ci.Status == ContentStatus.Published);

        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            query = query.Where(ci => ci.ContentType == request.ContentType);
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            query = query.Where(ci => ci.Tags.Any(t => t.ContentTag.Slug == request.Tag || t.ContentTag.Name == request.Tag));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderBy(ci => ci.SortOrder)
            .ThenByDescending(ci => ci.PublishedAt ?? ci.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ci => ci.ToDto())
            .ToListAsync(cancellationToken);

        return Result.Success(PaginatedResult<ContentItemDto>.Create(items, page, pageSize, totalCount));
    }
}
