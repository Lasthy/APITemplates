using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Tags;

/// <summary>
/// Query to get tag by ID.
/// </summary>
public record GetTag(DefaultId Id) : IRequest<ContentTagDto>;

[Logging(Order = 1)]
public class GetTagHandler : IRequestHandler<GetTag, ContentTagDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public GetTagHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentTagDto>> HandleAsync(GetTag request, CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tag == null)
        {
            return Result.Failure<ContentTagDto>(new KeyNotFoundException($"Tag '{request.Id}' not found."));
        }

        return Result.Success(tag.ToDto());
    }
}
