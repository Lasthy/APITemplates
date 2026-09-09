using Cms.Contracts.Dtos;
using Cms.Entities;
using Cms.Persistence;
using Cms.Services;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Cms.Features.Tags;

/// <summary>
/// Command to create a new content tag.
/// </summary>
public record CreateTag(
    string Name,
    string? Slug = null,
    string? Description = null) : IRequest<ContentTagDto>;

[Logging(Order = 1)]
public class CreateTagHandler : IRequestHandler<CreateTag, ContentTagDto>
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly ISlugService _slugService;

    public CreateTagHandler(CmsModuleDbContext dbContext, ISlugService slugService)
    {
        _dbContext = dbContext;
        _slugService = slugService;
    }

    public async Task<Result<ContentTagDto>> HandleAsync(CreateTag request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<ContentTagDto>(new ArgumentException("Tag name is required."));
        }

        var slug = !string.IsNullOrWhiteSpace(request.Slug)
            ? _slugService.Generate(request.Slug)
            : _slugService.Generate(request.Name);

        var existing = await _dbContext.Tags
            .AnyAsync(t => t.Slug == slug, cancellationToken);

        if (existing)
        {
            return Result.Failure<ContentTagDto>(new InvalidOperationException($"Tag with slug '{slug}' already exists."));
        }

        var tag = new ContentTag
        {
            Id = DefaultId.NewUlid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(tag.ToDto());
    }
}
