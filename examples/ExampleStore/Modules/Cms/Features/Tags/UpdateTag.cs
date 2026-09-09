using Cms.Contracts.Dtos;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Tags;

/// <summary>
/// Command to update tag name and description.
/// </summary>
public record UpdateTag(
    DefaultId Id,
    string? Name = null,
    string? Description = null) : IRequest<ContentTagDto>;

[Logging(Order = 1)]
public class UpdateTagHandler : IRequestHandler<UpdateTag, ContentTagDto>
{
    private readonly CmsModuleDbContext _dbContext;

    public UpdateTagHandler(CmsModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ContentTagDto>> HandleAsync(UpdateTag request, CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag == null)
        {
            return Result.Failure<ContentTagDto>(new KeyNotFoundException($"Tag '{request.Id}' not found."));
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            tag.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            tag.Description = request.Description.Trim();
        }

        tag.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(tag.ToDto());
    }
}
