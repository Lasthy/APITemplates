using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Tags;

/// <summary>
/// Query to get paged tags.
/// </summary>
public record GetTags(int Page = 1, int PageSize = 20) : IRequest<PaginatedResult<ContentTagDto>>;

[Logging(Order = 1)]
public class GetTagsHandler : IRequestHandler<GetTags, PaginatedResult<ContentTagDto>>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetTagsHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<ContentTagDto>>> HandleAsync(GetTags request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _dbContext.Tags.AsNoTracking().OrderBy(t => t.Name);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => t.ToDto())
            .ToListAsync(cancellationToken);

        return Result.Success(PaginatedResult<ContentTagDto>.Create(items, page, pageSize, totalCount));
    }
}
