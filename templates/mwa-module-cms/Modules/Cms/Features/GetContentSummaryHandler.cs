using Cms.Contracts.Dtos;
using Cms.Contracts.Requests;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features;

/// <summary>
/// Cross-module handler for <see cref="GetContentSummary"/>.
/// Allows other modules to query content summary information by content ID.
/// </summary>
[Logging(Order = 1)]
public class GetContentSummaryHandler : IRequestHandler<GetContentSummary, ContentItemDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetContentSummaryHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentItemDto>> HandleAsync(GetContentSummary request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ContentItems
            .AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.Id == request.ContentId && ci.DeletedAt == null, cancellationToken);

        if (item == null)
        {
            return Result.Failure<ContentItemDto>(
                new KeyNotFoundException($"Content item '{request.ContentId}' not found."));
        }

        return Result.Success(item.ToDto());
    }
}
