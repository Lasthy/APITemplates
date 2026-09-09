using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Tags;

/// <summary>
/// Command to delete a tag, removing it from all associated content items.
/// </summary>
public record DeleteTag(DefaultId Id) : IRequest<bool>;

[Logging(Order = 1)]
public class DeleteTagHandler : IRequestHandler<DeleteTag, bool>
{
    private readonly CmsModuleDbContext _dbContext;

    public DeleteTagHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> HandleAsync(DeleteTag request, CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag == null)
        {
            return Result.Failure<bool>(new KeyNotFoundException($"Tag '{request.Id}' not found."));
        }

        var joins = await _dbContext.ContentItemTags.Where(cit => cit.ContentTagId == request.Id).ToListAsync(cancellationToken);
        _dbContext.ContentItemTags.RemoveRange(joins);
        _dbContext.Tags.Remove(tag);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
