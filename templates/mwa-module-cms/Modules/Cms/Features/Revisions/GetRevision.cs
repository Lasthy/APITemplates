using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Revisions;

/// <summary>
/// Query to get full snapshot details for a specific revision of a content item.
/// </summary>
public record GetRevision(DefaultId ContentId, int RevisionNumber) : IRequest<ContentRevisionDetailDto>;

[Logging(Order = 1)]
public class GetRevisionHandler : IRequestHandler<GetRevision, ContentRevisionDetailDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetRevisionHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentRevisionDetailDto>> HandleAsync(GetRevision request, CancellationToken cancellationToken = default)
    {
        var revision = await _dbContext.Revisions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ContentItemId == request.ContentId && r.RevisionNumber == request.RevisionNumber, cancellationToken);

        if (revision == null)
        {
            return Result.Failure<ContentRevisionDetailDto>(
                new KeyNotFoundException($"Revision {request.RevisionNumber} not found for content item '{request.ContentId}'."));
        }

        return Result.Success(revision.ToDetailDto());
    }
}
