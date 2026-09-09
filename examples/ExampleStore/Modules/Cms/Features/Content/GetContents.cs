using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Content;

/// <summary>
/// Query to list content items with optional filters and pagination.
/// </summary>
public record GetContents(
    string? ContentType = null,
    string? Status = null,
    string? Tag = null,
    DefaultId? AuthorId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Descending) : IRequest<PaginatedResult<ContentItemDto>>;

[Logging(Order = 1)]
public class GetContentsHandler : IRequestHandler<GetContents, PaginatedResult<ContentItemDto>>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetContentsHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<ContentItemDto>>> HandleAsync(GetContents request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ContentItems
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            query = query.Where(ci => ci.ContentType == request.ContentType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(ci => ci.Status == request.Status);
        }

        if (request.AuthorId.HasValue && request.AuthorId.Value != default)
        {
            query = query.Where(ci => ci.AuthorId == request.AuthorId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(ci => ci.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(ci => ci.CreatedAt <= request.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            query = query.Where(ci => ci.Tags.Any(t => t.ContentTag.Slug == request.Tag || t.ContentTag.Name == request.Tag));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = (request.SortBy?.ToLowerInvariant(), request.SortDirection) switch
        {
            ("title", SortDirection.Ascending) => query.OrderBy(ci => ci.Title),
            ("title", SortDirection.Descending) => query.OrderByDescending(ci => ci.Title),
            ("sortorder", SortDirection.Ascending) => query.OrderBy(ci => ci.SortOrder),
            ("sortorder", SortDirection.Descending) => query.OrderByDescending(ci => ci.SortOrder),
            ("publishedat", SortDirection.Ascending) => query.OrderBy(ci => ci.PublishedAt),
            ("publishedat", SortDirection.Descending) => query.OrderByDescending(ci => ci.PublishedAt),
            (_, SortDirection.Ascending) => query.OrderBy(ci => ci.CreatedAt),
            _ => query.OrderByDescending(ci => ci.CreatedAt)
        };

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ci => ci.ToDto())
            .ToListAsync(cancellationToken);

        var paginated = PaginatedResult<ContentItemDto>.Create(items, page, pageSize, totalCount);
        return Result.Success(paginated);
    }
}
