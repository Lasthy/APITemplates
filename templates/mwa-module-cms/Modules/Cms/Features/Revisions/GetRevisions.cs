using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Revisions;

/// <summary>
/// Query to list revision summaries for a content item.
/// </summary>
public record GetRevisions(DefaultId ContentId) : IRequest<IReadOnlyList<ContentRevisionDto>>;

[Logging(Order = 1)]
public class GetRevisionsHandler : IRequestHandler<GetRevisions, IReadOnlyList<ContentRevisionDto>>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetRevisionsHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<ContentRevisionDto>>> HandleAsync(GetRevisions request, CancellationToken cancellationToken = default)
    {
        var revisions = await _dbContext.Revisions
            .AsNoTracking()
            .Where(r => r.ContentItemId == request.ContentId)
            .OrderByDescending(r => r.RevisionNumber)
            .Select(r => r.ToDto())
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ContentRevisionDto>>(revisions);
    }
}
