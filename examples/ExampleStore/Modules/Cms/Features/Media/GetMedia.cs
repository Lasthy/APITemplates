using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Media;

/// <summary>
/// Query to list media records with optional filters and pagination.
/// </summary>
public record GetMedia(
    DefaultId? ContentItemId = null,
    string? MimeType = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<ContentMediaDto>>;

[Logging(Order = 1)]
public class GetMediaHandler : IRequestHandler<GetMedia, PaginatedResult<ContentMediaDto>>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetMediaHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<ContentMediaDto>>> HandleAsync(GetMedia request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Media.AsNoTracking().AsQueryable();

        if (request.ContentItemId.HasValue && request.ContentItemId.Value != default)
        {
            query = query.Where(m => m.ContentItemId == request.ContentItemId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.MimeType))
        {
            query = query.Where(m => m.MimeType == request.MimeType || m.MimeType.StartsWith(request.MimeType));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => m.ToDto())
            .ToListAsync(cancellationToken);

        return Result.Success(PaginatedResult<ContentMediaDto>.Create(items, page, pageSize, totalCount));
    }
}
